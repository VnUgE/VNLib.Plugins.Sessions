﻿/*
* Copyright (c) 2022 Vaughn Nugent
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

using Microsoft.EntityFrameworkCore;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Data.Caching.Exceptions;
using VNLib.Plugins.Sessions.Cache.Client;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Oauth;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Oauth.Tokens;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Extensions.Loading.Events;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Sessions.OAuth
{

    /// <summary>
    /// Provides OAuth2 session management
    /// </summary>
    [ConfigurationName("oauth2")]
    internal sealed class OAuth2SessionProvider : SessionCacheClient, ITokenManager, IIntervalScheduleable
    {

        private static readonly SessionHandle NotFoundHandle = new(null, FileProcessArgs.NotFound, null);
        
        private static readonly TimeSpan BackgroundTimeout = TimeSpan.FromSeconds(10);

        
        private readonly IOauthSessionIdFactory factory;
        private readonly TokenStore TokenStore;
        private readonly uint MaxConnections;
        
        public OAuth2SessionProvider(IRemoteCacheStore client, int maxCacheItems, uint maxConnections, IOauthSessionIdFactory idFactory, DbContextOptions dbCtx)
            : base(client, maxCacheItems)
        {
            factory = idFactory;
            TokenStore = new(dbCtx);
            MaxConnections = maxConnections;
        }

        ///<inheritdoc/>
        protected override RemoteSession SessionCtor(string sessionId) => new OAuth2Session(sessionId, Store, BackgroundTimeout, InvalidatateCache);

        private void InvalidatateCache(OAuth2Session session)
        {
            lock (CacheLock)
            {
                _ = CacheTable.Remove(session.SessionID);
            }
        }

        ///<inheritdoc/>
        public async ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            //Callback to close the session when the handle is closeed
            static ValueTask HandleClosedAsync(ISession session, IHttpEvent entity)
            {
                return ((SessionBase)session).UpdateAndRelease(true, entity);
            }
            try
            {
                //Get session id
                if (!factory.TryGetSessionId(entity, out string? sessionId))
                {
                    //Id not allowed/found, so do not attach a session
                    return SessionHandle.Empty;
                }

                //Limit max number of waiting clients
                if (WaitingConnections > MaxConnections)
                {
                    //Set 503 for temporary unavail
                    entity.CloseResponse(HttpStatusCode.ServiceUnavailable);
                    return new SessionHandle(null, FileProcessArgs.VirtualSkip, null);
                }

                //Recover the session
                RemoteSession session = await base.GetSessionAsync(entity, sessionId, cancellationToken);
                
                //Session should not be new
                if (session.IsNew)
                {
                    //Invalidate the session, so it is deleted
                    session.Invalidate();
                    await session.UpdateAndRelease(true, entity);
                    return SessionHandle.Empty;
                }
                //Make sure session is still valid
                if (session.Created.Add(factory.SessionValidFor) < DateTimeOffset.UtcNow)
                {
                    //Invalidate the handle
                    session.Invalidate();
                    //Flush changes
                    await session.UpdateAndRelease(false, entity);
                    //Remove the token from the db backing store
                    await TokenStore.RevokeTokenAsync(sessionId, cancellationToken);
                    //close entity
                    entity.CloseResponseError(HttpStatusCode.Unauthorized, ErrorType.InvalidToken, "The token has expired");
                    //return a completed handle
                    return NotFoundHandle;
                }
                
                return new SessionHandle(session, HandleClosedAsync);
            }
            //Pass session exceptions
            catch (SessionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SessionException("Exception raised while retreiving or loading OAuth2 session", ex);
            }
        }
        ///<inheritdoc/>
        public async Task<IOAuth2TokenResult?> CreateAccessTokenAsync(IHttpEvent ev, UserApplication app, CancellationToken cancellation)
        {
            //Get a new session for the current connection
            TokenAndSessionIdResult ids = factory.GenerateTokensAndId();
            //try to insert token into the store, may fail if max has been reached
            if (await TokenStore.InsertTokenAsync(ids.SessionId, app.Id!, ids.RefreshToken, factory.MaxTokensPerApp, cancellation) != ERRNO.SUCCESS)
            {
                return null;
            }
            //Create new session from the session id
            RemoteSession session = SessionCtor(ids.SessionId);
            await session.WaitAndLoadAsync(ev, cancellation);
            try
            {
                //Init new session
                factory.InitNewSession(session, app, ev);
            }
            finally
            {
                await session.UpdateAndRelease(false, ev);
            }
            //Init new token result to pass to client
            return new OAuth2TokenResult()
            {
                ExpiresSeconds = (int)factory.SessionValidFor.TotalSeconds,
                TokenType = factory.TokenType,
                //Return token and refresh token
                AccessToken = ids.AccessToken,
                RefreshToken = ids.RefreshToken,
            };
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

        ///<inheritdoc/>
        async Task IIntervalScheduleable.OnIntervalAsync(ILogProvider log, CancellationToken cancellationToken)
        {
            //Calculate valid token time
            DateTime validAfter = DateTime.UtcNow.Subtract(factory.SessionValidFor);
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
                    await base.Store.DeleteObjectAsync(token.Id, cancellationToken);
                }
                //Ignore if the object has already been removed
                catch (ObjectNotFoundException)
                {}
                catch (Exception ex)
                {
                    errors = new()
                    {
                        ex
                    };
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