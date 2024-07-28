/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: OAuth2SessionProvider.cs 
*
* OAuth2SessionProvider.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth 
* which is part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.OAuth is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.OAuth is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Oauth.Tokens;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Loading.Events;
using VNLib.Plugins.Extensions.Loading.Routing;
using VNLib.Plugins.Sessions.OAuth.Endpoints;
using static VNLib.Plugins.Essentials.Oauth.OauthSessionExtensions;

namespace VNLib.Plugins.Sessions.OAuth
{

    /// <summary>
    /// Provides OAuth2 session management
    /// </summary>
    [ServiceExport]
    [ConfigurationName(OAUTH2_CONFIG_KEY)]
    public sealed class OAuth2SessionProvider : ISessionProvider, ITokenManager, IApplicationTokenFactory, IIntervalScheduleable
    {
        public const string OAUTH2_CONFIG_KEY = "oauth2";

        private static readonly SessionHandle Skip = new(null, FileProcessArgs.VirtualSkip, null);

        private readonly OAuth2SessionStore _sessions;
        private readonly IOauthSessionIdFactory _tokenFactory;
        private readonly TokenStore TokenStore;
        private readonly string _tokenTypeString;
        private readonly uint _maxConnections;

        private uint _waitingConnections;

        public OAuth2SessionProvider(PluginBase plugin, IConfigScope config)
        {
            _sessions = plugin.GetOrCreateSingleton<OAuth2SessionStore>();
            _tokenFactory = plugin.GetOrCreateSingleton<OAuth2TokenFactory>();
            TokenStore = new(plugin.GetContextOptions());
            _tokenTypeString = $"client_credential,{_tokenFactory.TokenType}";

            _maxConnections = config.GetValueOrDefault("max_connections", p => p.GetUInt32(), 1000u);

            //Schedule interval
            plugin.ScheduleInterval(this, TimeSpan.FromMinutes(2));

            /*
             * Route built-in oauth2 endpoints 
             */
            if (config.ContainsKey("token_path"))
            {
                /*
                 * Access token endpoint requires this instance as a token manager
                 * which would cause a circular dependency, so it needs to be routed
                 * manually
                 */
                AccessTokenEndpoint tokenEndpoint = new(plugin, config, this);
                //Create token endpoint
                plugin.Route(tokenEndpoint);
            }

            //Optional revocation endpoint
            if (plugin.HasConfigForType<RevocationEndpoint>())
            {
                //Route revocation endpoint
                plugin.Route<RevocationEndpoint>();
            }
        }

        /*
         * Called in SessionProvider.dll to check if the current request can be processed
         * as an oauth2 session
         */
        public bool CanProcess(IHttpEvent entity)
        {
            //If authorization header is set try to process as oauth2 session
            return _sessions.IsConnected && entity.Server.Headers.HeaderSet(HttpRequestHeader.Authorization);
        }

        ///<inheritdoc/>
        public ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            //Limit max number of waiting clients and make sure were connected
            if (!_sessions.IsConnected || _waitingConnections > _maxConnections)
            {
                //Set 503 for temporary unavail
                entity.CloseResponse(HttpStatusCode.ServiceUnavailable);
                return ValueTask.FromResult(Skip);
            }

            ValueTask<OAuth2Session?> result = _sessions.GetSessionAsync(entity, cancellationToken);

            if (result.IsCompleted)
            {
                //Safe to get result synchronously
                OAuth2Session? session = result.GetAwaiter().GetResult();

                //Post process and get handle for session
                SessionHandle handle = PostProcess(session);

                return ValueTask.FromResult(handle);
            }
            else
            {
                return new(AwaitAsyncGet(result));
            }
        }

        private async Task<SessionHandle> AwaitAsyncGet(ValueTask<OAuth2Session?> async)
        {
            //Inct wait count while async waiting
            Interlocked.Increment(ref _waitingConnections);
            try
            {
                //await the session
                OAuth2Session? session = await async.ConfigureAwait(false);

                //return empty session handle if the session could not be found
                return PostProcess(session);
            }
            finally
            {
                Interlocked.Decrement(ref _waitingConnections);
            }
        }

        private SessionHandle PostProcess(OAuth2Session? session)
        {
            if (session is null)
            {
                return SessionHandle.Empty;
            }

            //Make sure the session has not expired yet
            if (session.Created.Add(_tokenFactory.SessionValidFor) < DateTimeOffset.UtcNow)
            {
                //Invalidate the session, so its technically valid for this request, but will be cleared on this handle close cycle
                session.Invalidate();

                //Clears important security variables
                InitNewSession(session, app: null);
            }

            return new SessionHandle(session, OnSessionReleases);
        }

        private ValueTask OnSessionReleases(ISession session, IHttpEvent entity) => _sessions.ReleaseSessionAsync((OAuth2Session)session, entity);
       
        ///<inheritdoc/>
        public async Task<IOAuth2TokenResult?> CreateAccessTokenAsync(IHttpEvent ev, UserApplication app, CancellationToken cancellation)
        {
            //Get a new session for the current connection
            GetTokenResult ids = _tokenFactory.GenerateTokensAndId();

            //try to insert token into the store, may fail if max has been reached
            if (await TokenStore.InsertTokenAsync(ids.AccessToken, app.Id!, ids.RefreshToken, _tokenFactory.MaxTokensPerApp, cancellation) != ERRNO.SUCCESS)
            {
                return null;
            }

            //Create new session
            OAuth2Session newSession = _sessions.CreateSession(ev, ids.AccessToken);

            //Init the new session with application information
            InitNewSession(newSession, app);

            //Commit the new session
            await _sessions.CommitSessionAsync(newSession);

            //Init new token result to pass to client
            return new OAuth2TokenResult
            {
                ExpiresSeconds = (int)_tokenFactory.SessionValidFor.TotalSeconds,
                TokenType = _tokenFactory.TokenType,
                //Return token and refresh token
                AccessToken = ids.AccessToken,
                RefreshToken = ids.RefreshToken,
            };
        }

        private void InitNewSession(OAuth2Session session, UserApplication? app)
        {
            //Store session variables
            session[APP_ID_ENTRY] = app?.Id;
            session[TOKEN_TYPE_ENTRY] = _tokenTypeString;
            session[SCOPES_ENTRY] = app?.Permissions;
            session.UserID = app?.UserId;
        }

        ///<inheritdoc/>
        Task ITokenManager.RevokeTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellation)
        {
            return TokenStore.RevokeTokensAsync(tokens, cancellation);
        }

        ///<inheritdoc/>
        Task ITokenManager.RevokeTokensForAppAsync(string appId, CancellationToken cancellation)
        {
            return TokenStore.RevokeTokenAsync(appId, cancellation);
        }


        /*
         * Interval for removing expired tokens
         */
       
        public async Task OnIntervalAsync(ILogProvider log, CancellationToken cancellationToken)
        {
            //Calculate valid token time
            DateTime validAfter = DateTime.UtcNow.Subtract(_tokenFactory.SessionValidFor);
            //Remove tokens from db store
            IReadOnlyCollection<ActiveToken> revoked = await TokenStore.CleanupExpiredTokensAsync(validAfter, cancellationToken);
            //exception list
            List<Exception>? errors = null;
            //Remove all sessions from the store
            foreach (ActiveToken token in revoked)
            {
                try
                {
                    //Remove tokens by thier object id from cache
                    await _sessions.DeleteTokenAsync(token.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    errors ??= [];
                    errors.Add(ex);
                }
            }
            if (errors?.Count > 0)
            {
                throw new AggregateException(errors);
            }
            if(revoked.Count > 0)
            {
                log.Debug("Cleaned up {0} expired tokens", revoked.Count);
            }
        }
    }
}
