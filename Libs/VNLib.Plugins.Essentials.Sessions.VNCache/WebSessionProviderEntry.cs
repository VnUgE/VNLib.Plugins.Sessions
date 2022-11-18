using System.Text.Json;

using VNLib.Net.Http;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Essentials.Sessions.Runtime;

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
            _ = WokerDoWorkAsync(plugin, localized, idFactory, cacheConfig, webSessionConfig);
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