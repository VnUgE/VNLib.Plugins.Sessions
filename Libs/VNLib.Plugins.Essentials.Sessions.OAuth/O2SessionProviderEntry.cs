/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: O2SessionProviderEntry.cs 
*
* O2SessionProviderEntry.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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

using System.Text.Json;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Oauth.Tokens;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Essentials.Sessions.OAuth;
using VNLib.Plugins.Essentials.Sessions.OAuth.Endpoints;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Routing;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Loading.Events;
using VNLib.Plugins.Essentials.Sessions.Runtime;
using VNLib.Data.Caching.Extensions;

namespace VNLib.Plugins.Essentials.Sessions.Oauth
{

    public sealed class O2SessionProviderEntry : IRuntimeSessionProvider
    {
        const string VNCACHE_CONFIG_KEY = "vncache";
        const string OAUTH2_CONFIG_KEY = "oauth2";

        private OAuth2SessionProvider? _sessions;

        bool IRuntimeSessionProvider.CanProcess(IHttpEvent entity)
        {
            //If authorization header is set try to process as oauth2 session
            return _sessions != null && entity.Server.Headers.HeaderSet(System.Net.HttpRequestHeader.Authorization);
        }

        ValueTask<SessionHandle> ISessionProvider.GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return _sessions!.GetSessionAsync(entity, cancellationToken);
        }
        

        void IRuntimeSessionProvider.Load(PluginBase plugin, ILogProvider localized)
        {
            //Try get vncache config element
            IReadOnlyDictionary<string, JsonElement> cacheConfig = plugin.GetConfig(VNCACHE_CONFIG_KEY);
            
            IReadOnlyDictionary<string, JsonElement> oauth2Config = plugin.GetConfig(OAUTH2_CONFIG_KEY);

            //Optional application jwt token 
            Task<JsonDocument?> jwtTokenSecret = plugin.TryGetSecretAsync("application_token_key")
                .ContinueWith(static t => t.Result == null ? null : JsonDocument.Parse(t.Result), TaskScheduler.Default);

            //Access token endpoint is optional
            if (oauth2Config.TryGetValue("token_path", out JsonElement el))
            {
                //Init auth endpoint
                AccessTokenEndpoint authEp = new(el.GetString()!, plugin, CreateTokenDelegateAsync, jwtTokenSecret);

                //route auth endpoint
                plugin.Route(authEp);
            }

            //Optional revocation endpoint
            if (plugin.HasConfigForType<RevocationEndpoint>())
            {
                //Route revocation endpoint
                plugin.Route<RevocationEndpoint>();
            }

            //Run
            _ = plugin.DeferTask(() => CacheWokerDoWorkAsync(plugin, localized, cacheConfig, oauth2Config), 100);
            
        }

        private async Task<IOAuth2TokenResult?> CreateTokenDelegateAsync(HttpEntity entity, UserApplication app, CancellationToken cancellation)
        {
            return await _sessions!.CreateAccessTokenAsync(entity, app, cancellation).ConfigureAwait(false);
        }

        /*
         * Starts and monitors the VNCache connection
         */

        private async Task CacheWokerDoWorkAsync(PluginBase plugin, ILogProvider localized, 
            IReadOnlyDictionary<string, JsonElement> cacheConfig, 
            IReadOnlyDictionary<string, JsonElement> oauth2Config)
        {
            //Init cache client
            using VnCacheClient cache = new(plugin.IsDebug() ? plugin.Log : null, Utils.Memory.Memory.Shared);
            
            try
            {
                int cacheLimit = oauth2Config["cache_size"].GetInt32();
                int maxTokensPerApp = oauth2Config["max_tokens_per_app"].GetInt32();
                int sessionIdSize = (int)oauth2Config["access_token_size"].GetUInt32();
                TimeSpan tokenValidFor = oauth2Config["token_valid_for_sec"].GetTimeSpan(TimeParseType.Seconds);
                TimeSpan cleanupInterval = oauth2Config["gc_interval_sec"].GetTimeSpan(TimeParseType.Seconds);
                string sessionIdPrefix = oauth2Config["cache_prefix"].GetString() ?? throw new KeyNotFoundException($"Missing required key 'cache_prefix' in '{OAUTH2_CONFIG_KEY}' config");

                //init the id provider
                OAuth2SessionIdProvider idProv = new(sessionIdPrefix, maxTokensPerApp, sessionIdSize, tokenValidFor);

                //Try loading config
                await cache.LoadConfigAsync(plugin, cacheConfig);

                //Init session provider now that client is loaded
                _sessions = new(cache.Resource!, cacheLimit, idProv, plugin.GetContextOptions());

                //Schedule cleanup interval with the plugin scheduler
                plugin.ScheduleInterval(_sessions, cleanupInterval);

                localized.Information("Session provider loaded");

                //Run and wait for exit
                await cache.RunAsync(localized, plugin.UnloadToken);

            }
            catch (OperationCanceledException)
            {}
            catch (KeyNotFoundException e)
            {
                localized.Error("Missing required configuration variable for VnCache client: {0}", e.Message);
            }
            catch(FBMServerNegiationException fne)
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