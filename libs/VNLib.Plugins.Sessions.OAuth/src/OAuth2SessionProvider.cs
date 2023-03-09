/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: OAuth2SessionProvider.cs 
*
* OAuth2SessionProvider.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
* VNLib collection of libraries and utilities.
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
using VNLib.Data.Caching.Exceptions;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Oauth.Tokens;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Loading.Events;
using static VNLib.Plugins.Essentials.Oauth.OauthSessionExtensions;

namespace VNLib.Plugins.Sessions.OAuth
{

    /// <summary>
    /// Provides OAuth2 session management
    /// </summary>
    [ConfigurationName(O2SessionProviderEntry.OAUTH2_CONFIG_KEY)]
    internal sealed class OAuth2SessionProvider : ISessionProvider, ITokenManager, IApplicationTokenFactory
    {        
        private static readonly SessionHandle Skip = new(null, FileProcessArgs.VirtualSkip, null);

        private readonly OAuth2SessionStore _sessions;
        private readonly IOauthSessionIdFactory _tokenFactory;
        private readonly TokenStore TokenStore;
        private readonly string _tokenTypeString;
        private readonly uint _maxConnections;        

        private uint _waitingConnections;

        public bool IsConnected => _sessions.IsConnected;

        public OAuth2SessionProvider(PluginBase plugin, IConfigScope config)
        {
            _sessions = plugin.GetOrCreateSingleton<OAuth2SessionStore>();
            _tokenFactory = plugin.GetOrCreateSingleton<OAuth2TokenFactory>();
            TokenStore = new(plugin.GetContextOptions());
            _tokenTypeString = $"client_credential,{_tokenFactory.TokenType}";
        }

        public void SetLog(ILogProvider log) => _sessions.SetLog(log);

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
            _waitingConnections++;
            try
            {
                //await the session
                OAuth2Session? session = await async.ConfigureAwait(false);

                //return empty session handle if the session could not be found
                return PostProcess(session);
            }
            finally
            {
                _waitingConnections--;
            }
        }

        private SessionHandle PostProcess(OAuth2Session? session)
        {
            if (session == null)
            {
                return SessionHandle.Empty;
            }

            //Make sure the session has not expired yet
            if (session.Created.Add(_tokenFactory.SessionValidFor) < DateTimeOffset.UtcNow)
            {
                //Invalidate the session, so its technically valid for this request, but will be cleared on this handle close cycle
                session.Invalidate();

                //Clears important security variables
                InitNewSession(session, null);
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
            return new OAuth2TokenResult()
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

        [AsyncInterval(Minutes = 2)]
        private async Task OnIntervalAsync(ILogProvider log, CancellationToken cancellationToken)
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
                //Ignore if the object has already been removed
                catch (ObjectNotFoundException)
                {}
                catch (Exception ex)
                {
#pragma warning disable CA1508 // Avoid dead conditional code
                    errors ??= new();
#pragma warning restore CA1508 // Avoid dead conditional code
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
