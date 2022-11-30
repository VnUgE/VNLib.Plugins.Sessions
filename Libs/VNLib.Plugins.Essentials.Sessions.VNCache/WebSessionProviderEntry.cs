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

using System.Text.Json;

using VNLib.Net.Http;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Essentials.Sessions.Runtime;
using VNLib.Data.Caching.Extensions;

namespace VNLib.Plugins.Essentials.Sessions.VNCache
{
    public sealed class WebSessionProviderEntry : IRuntimeSessionProvider
    {
        const string VNCACHE_CONFIG_KEY = "vncache";
        const string WEB_SESSION_CONFIG = "web";

        private WebSessionProvider? _sessions;

        public bool CanProcess(IHttpEvent entity)
        {
            //Web sessions can always be provided so long as cache is loaded
            return _sessions != null;
        }

        public ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return _sessions!.GetSessionAsync(entity, cancellationToken);
        }

        void IRuntimeSessionProvider.Load(PluginBase plugin, ILogProvider localized)
        {
            //Try get vncache config element
            IReadOnlyDictionary<string, JsonElement> cacheConfig = plugin.GetConfig(VNCACHE_CONFIG_KEY);

            IReadOnlyDictionary<string, JsonElement> webSessionConfig = plugin.GetConfig(WEB_SESSION_CONFIG);

            uint cookieSize = webSessionConfig["cookie_size"].GetUInt32();
            string cookieName = webSessionConfig["cookie_name"].GetString() ?? throw new KeyNotFoundException($"Missing required element 'cookie_name' for config '{WEB_SESSION_CONFIG}'");
            string cachePrefix = webSessionConfig["cache_prefix"].GetString() ?? throw new KeyNotFoundException($"Missing required element 'cache_prefix' for config '{WEB_SESSION_CONFIG}'");
            TimeSpan validFor = webSessionConfig["valid_for_sec"].GetTimeSpan(TimeParseType.Seconds);

            //Init id factory
            WebSessionIdFactoryImpl idFactory = new(cookieSize, cookieName, cachePrefix, validFor);

            //Run client connection
            _ = plugin.DeferTask(() => WokerDoWorkAsync(plugin, localized, idFactory, cacheConfig, webSessionConfig)); 
        }

       
        /*
        * Starts and monitors the VNCache connection
        */

        private async Task WokerDoWorkAsync(
            PluginBase plugin,
            ILogProvider localized,
            WebSessionIdFactoryImpl idFactory,
            IReadOnlyDictionary<string, JsonElement> cacheConfig,
            IReadOnlyDictionary<string, JsonElement> webSessionConfig)
        {
            //Init cache client
            using VnCacheClient cache = new(plugin.IsDebug() ? plugin.Log : null, Memory.Shared);

            try
            {
                int cacheLimit = (int)webSessionConfig["cache_size"].GetUInt32();
                uint maxConnections = webSessionConfig["max_waiting_connections"].GetUInt32();

                //Try loading config
                await cache.LoadConfigAsync(plugin, cacheConfig);

                //Init provider
                _sessions = new(cache.Resource!, cacheLimit, maxConnections, idFactory);


                localized.Information("Session provider loaded");

                //Run and wait for exit
                await cache.RunAsync(localized, plugin.UnloadToken);

            }
            catch (OperationCanceledException)
            { }
            catch (KeyNotFoundException e)
            {
                localized.Error("Missing required configuration variable for VnCache client: {0}", e.Message);
            }
            catch (FBMServerNegiationException fne)
            {
                localized.Error("Failed to negotiate connection with cache server {reason}", fne.Message);
            }
            catch (Exception ex)
            {
                localized.Error(ex, "Cache client error occured in session provider");
            }
            finally
            {
                _sessions = null;
            }

            localized.Information("Cache client exited");
        }
    }
}