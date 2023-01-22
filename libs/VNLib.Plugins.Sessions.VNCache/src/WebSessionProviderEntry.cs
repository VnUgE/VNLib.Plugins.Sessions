/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: WebSessionProviderEntry.cs 
*
* WebSessionProviderEntry.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Data.Caching;
using VNLib.Plugins.Sessions.Cache.Client;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.VNCache;
using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Sessions.VNCache
{
    public sealed class WebSessionProviderEntry : ISessionProvider
    {
        const string WEB_SESSION_CONFIG = "web";

        private WebSessionProvider? _sessions;

        //Web sessions can always be provided so long as cache is loaded
        public bool CanProcess(IHttpEvent entity) => _sessions != null;

        ValueTask<SessionHandle> ISessionProvider.GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return _sessions!.GetSessionAsync(entity, cancellationToken);
        }

        public void Load(PluginBase plugin, ILogProvider localized)
        {
            //Try get vncache config element
            IReadOnlyDictionary<string, JsonElement> webSessionConfig = plugin.GetConfigForType<WebSessionProvider>();

            uint cookieSize = webSessionConfig["cookie_size"].GetUInt32();
            string cookieName = webSessionConfig["cookie_name"].GetString() ?? throw new KeyNotFoundException($"Missing required element 'cookie_name' for config '{WEB_SESSION_CONFIG}'");
            string cachePrefix = webSessionConfig["cache_prefix"].GetString() ?? throw new KeyNotFoundException($"Missing required element 'cache_prefix' for config '{WEB_SESSION_CONFIG}'");
            int cacheLimit = (int)webSessionConfig["cache_size"].GetUInt32();
            uint maxConnections = webSessionConfig["max_waiting_connections"].GetUInt32();
            TimeSpan validFor = webSessionConfig["valid_for_sec"].GetTimeSpan(TimeParseType.Seconds);

            //Init id factory
            WebSessionIdFactoryImpl idFactory = new(cookieSize, cookieName, cachePrefix, validFor);

            //Get shared global-cache
            IGlobalCacheProvider globalCache = plugin.GetGlobalCache(localized);

            //Create cache store from global cache
            GlobalCacheStore cacheStore = new(globalCache);

            //Init provider
            _sessions = new(cacheStore, cacheLimit, maxConnections, idFactory);

            //Load and run cached sessions on deferred task lib
            _ = plugin.DeferTask(() => _sessions.CleanupExpiredSessionsAsync(localized, plugin.UnloadToken), 1000);

            localized.Information("Session provider loaded");
        }
    }
}