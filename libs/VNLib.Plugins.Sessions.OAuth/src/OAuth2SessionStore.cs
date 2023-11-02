/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: OAuth2SessionStore.cs 
*
* OAuth2SessionStore.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Hashing;
using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Data.Caching;
using VNLib.Plugins.Sessions.Cache.Client;
using VNLib.Plugins.Extensions.VNCache;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Sessions.OAuth
{
    [ConfigurationName(OAuth2SessionProvider.OAUTH2_CONFIG_KEY)]
    internal sealed class OAuth2SessionStore : SessionStore<OAuth2Session>
    {
        const int MAX_SESSION_BUFFER_SIZE = 16 * 1024;

        private ILogProvider _log;

        ///<inheritdoc/>
        protected override ISessionIdFactory IdFactory { get; }

        ///<inheritdoc/>
        protected override IRemoteCacheStore Cache { get; }

        ///<inheritdoc/>
        protected override ISessionFactory<OAuth2Session> SessionFactory { get; }

        ///<inheritdoc/>
        protected override ILogProvider Log => _log;

        public bool IsConnected => Cache.IsConnected;

        public OAuth2SessionStore(PluginBase plugin, IConfigScope config)
        {
            OAuth2SessionConfig o2Conf = config.DeserialzeAndValidate<OAuth2SessionConfig>();

            //Get global cache
            IGlobalCacheProvider? cache = plugin.GetDefaultGlobalCache()?
                .GetPrefixedCache(o2Conf.CachePrefix, HashAlg.SHA256);

            _ = cache ?? throw new MissingDependencyException("A global cache provider is required to store OAuth2 sessions. Please configure a cache provider");

            //Get debug log if enabled
            ILogProvider? sessDebugLog = plugin.HostArgs.HasArgument("--debug-sessions") ? plugin.Log.CreateScope("OAuth2-Sessions") : null;

            //Create cache store from global cache
            Cache = new GlobalCacheStore(cache, MAX_SESSION_BUFFER_SIZE, sessDebugLog);

            IdFactory = plugin.GetOrCreateSingleton<OAuth2TokenFactory>();

            SessionFactory = new OAuth2SessionFactory();

            //Default to plugin cache
            _log = plugin.Log;
        }

        public Task DeleteTokenAsync(string token, CancellationToken cancellation)
        {
            return Cache.DeleteObjectAsync(token, cancellation);
        }

        public OAuth2Session CreateSession(IHttpEvent entity, string sessionId)
        {
            //Get the new session
            OAuth2Session session = SessionFactory.GetNewSession(entity, sessionId, new Dictionary<string, string>(10));
            //Configure the new session for use
            session.InitNewSession(entity);

            return session;
        }

        public async Task CommitSessionAsync(OAuth2Session session)
        {
            IDictionary<string, string> sessionData = session.GetSessionData();
            //Write data to cache
            await Cache.AddOrUpdateObjectAsync(session.SessionID, null, sessionData);
            //Good programming, update session
            session.SessionUpdateComplete();
        }
    }
}