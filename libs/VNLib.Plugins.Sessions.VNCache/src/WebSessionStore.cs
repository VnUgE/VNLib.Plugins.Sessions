/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: WebSessionStore.cs 
*
* WebSessionStore.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
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
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Hashing;
using VNLib.Utils.Logging;
using VNLib.Data.Caching;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Sessions.Cache.Client;
using VNLib.Plugins.Extensions.VNCache;
using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Sessions.VNCache
{
    [ConfigurationName(WebSessionProviderEntry.WEB_SESSION_CONFIG)]
    internal sealed class WebSessionStore : SessionStore<WebSession>
    {
        const int MAX_SESSION_BUFFER_SIZE = 16 * 1024;

        private ILogProvider? baseLog;

        protected override ISessionIdFactory IdFactory { get; }
        protected override IRemoteCacheStore Cache { get; }
        protected override ISessionFactory<WebSession> SessionFactory { get; }
        protected override ILogProvider Log => baseLog!;

        public WebSessionStore(PluginBase pbase, IConfigScope config)
        {
            //Get id factory
            IdFactory = pbase.GetOrCreateSingleton<WebSessionIdFactory>();

            //Session factory
            SessionFactory = new WebSessionFactory();

            /*
             * Init prefixed cache, a prefix key is required from
             * the config
             */

            string cachePrefix = config["cache_prefix"].GetString() 
                ?? throw new KeyNotFoundException($"Missing required element 'cache_prefix' for config '{WebSessionProviderEntry.WEB_SESSION_CONFIG}'");

            //Create a simple prefix cache provider
            IGlobalCacheProvider cache = pbase.GetOrCreateSingleton<VnGlobalCache>()
                .GetPrefixedCache(cachePrefix, HashAlg.SHA256);

            //Create cache store from global cache
            Cache = new GlobalCacheStore(cache, MAX_SESSION_BUFFER_SIZE);

            //Default log to plugin log
            baseLog = pbase.Log;
        }

        public void InitLog(ILogProvider log) 
        {
            baseLog = log;
        }

        /// <summary>
        /// A value that indicates if the remote cache client is connected
        /// </summary>
        public bool IsConnected => Cache.IsConnected;

        public override ValueTask ReleaseSessionAsync(WebSession session, IHttpEvent entity)
        {
            //Get status flags first
            SessionStatus status = session.GetStatus();

            //If status is delete, we need to invalidate the session, and copy its security information
            if(status.HasFlag(SessionStatus.Delete))
            {
                //Run delete/cleanup
                Task delete = DeleteSessionAsync(session);

                //Regenid and create new session
                string newId = IdFactory.RegenerateId(entity);

                //Get new session empty session for the connection
                WebSession newSession = SessionFactory.GetNewSession(entity, newId, null);

                //Reset security information for new session
                newSession.InitNewSession(entity.Server);

                IDictionary<string, string> data = newSession.GetSessionData();

                //commit session to cache
                Task add = Cache.AddOrUpdateObjectAsync(newId, null, data);

                /*
                 * Call complete on session for good practice, this SHOULD be 
                 * called after the update has been awaited though.
                 * 
                 * We also do not need to use the mutal exclusion mechanism because
                 * no other connections should have this session's id yet.
                 */
                newSession.SessionUpdateComplete();
           
                //Await the invalidation async
                return new(AwaitInvalidate(delete, add));
            }
            else
            {
                return base.ReleaseSessionAsync(session, entity);
            }
        }

        private static async Task AwaitInvalidate(Task delete, Task addNew)
        {
            try
            {
                await Task.WhenAll(delete, addNew);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SessionException)
            {
                throw;
            }
            catch(Exception ex)
            {
                throw new SessionException("An exception occured during session invalidation", ex);
            }
        }
    }
}
