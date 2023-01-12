/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: WebSessionProvider.cs 
*
* WebSessionProvider.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.VNCache is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.VNCache is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Sessions.Cache.Client;

namespace VNLib.Plugins.Sessions.VNCache
{
    /// <summary>
    /// The implementation of a VNCache web based session
    /// </summary>
    [ConfigurationName("web")]
    internal sealed class WebSessionProvider : SessionCacheClient, ISessionProvider
    {
        static readonly TimeSpan BackgroundUpdateTimeout = TimeSpan.FromSeconds(10);

        private readonly IWebSessionIdFactory factory;
        private readonly uint MaxConnections;

        /// <summary>
        /// Initializes a new <see cref="WebSessionProvider"/> 
        /// </summary>
        /// <param name="client">The cache client to make cache operations against</param>
        /// <param name="maxCacheItems">The max number of items to store in cache</param>
        /// <param name="maxWaiting">The maxium number of waiting session events before 503s are sent</param>
        /// <param name="factory">The session-id factory</param>
        public WebSessionProvider(IRemoteCacheStore client, int maxCacheItems, uint maxWaiting, IWebSessionIdFactory factory) : base(client, maxCacheItems)
        {
            this.factory = factory;
            MaxConnections = maxWaiting;
        }

        private string UpdateSessionId(IHttpEvent entity, string oldId)
        {
            //Generate and set a new sessionid
            string newid = factory.GenerateSessionId(entity);
            //Aquire lock on cache
            lock (CacheLock)
            {
                //Change the cache lookup id
                if (CacheTable.Remove(oldId, out RemoteSession? session))
                {
                    CacheTable.Add(newid, session);
                }
            }
            return newid;
        }
        
        protected override RemoteSession SessionCtor(string sessionId) => new WebSession(sessionId, Store, BackgroundUpdateTimeout, UpdateSessionId);

        public async ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            //Callback to close the session when the handle is closeed
            static ValueTask HandleClosedAsync(ISession session, IHttpEvent entity)
            {
                return (session as SessionBase)!.UpdateAndRelease(true, entity);
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
                    entity.CloseResponse(System.Net.HttpStatusCode.ServiceUnavailable);
                    return new SessionHandle(null, FileProcessArgs.VirtualSkip, null);
                }

                //Get session
                RemoteSession session = await GetSessionAsync(entity, sessionId, cancellationToken);

                //If the session is new (not in cache), then overwrite the session id with a new one as user may have specified their own
                if (session.IsNew)
                {
                    session.RegenID();
                }

                //Make sure the session has not expired yet
                if (session.Created.Add(factory.ValidFor) < DateTimeOffset.UtcNow)
                {
                    //Invalidate the session, so its technically valid for this request, but will be cleared on this handle close cycle
                    session.Invalidate();
                    //Clear basic login status
                    session.Token = null;
                    session.UserID = null;
                    session.Privilages = 0;
                    session.SetLoginToken(null);
                }
                
                return new SessionHandle(session, HandleClosedAsync);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SessionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SessionException("Exception raised while retreiving or loading Web session", ex);
            }
        }
    }
}
