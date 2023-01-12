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
using VNLib.Data.Caching;
using VNLib.Plugins.Sessions.Cache.Client;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Oauth.Tokens;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Sessions.OAuth.Endpoints;
using VNLib.Plugins.Extensions.VNCache;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Routing;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Loading.Events;


namespace VNLib.Plugins.Sessions.OAuth
{

    public sealed class O2SessionProviderEntry : ISessionProvider
    {
        const string OAUTH2_CONFIG_KEY = "oauth2";

        private OAuth2SessionProvider? _sessions;

        public bool CanProcess(IHttpEvent entity)
        {
            //If authorization header is set try to process as oauth2 session
            return _sessions != null && entity.Server.Headers.HeaderSet(System.Net.HttpRequestHeader.Authorization);
        }

        ValueTask<SessionHandle> ISessionProvider.GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return _sessions!.GetSessionAsync(entity, cancellationToken);
        }
        

        public void Load(PluginBase plugin, ILogProvider localized)
        {
            IReadOnlyDictionary<string, JsonElement> oauth2Config = plugin.GetConfigForType<OAuth2SessionProvider>();

            //Access token endpoint is optional
            if (oauth2Config.TryGetValue("token_path", out JsonElement el))
            {
                //Init auth endpoint
                AccessTokenEndpoint authEp = new(el.GetString()!, plugin, CreateTokenDelegateAsync);

                //route auth endpoint
                plugin.Route(authEp);
            }

            //Optional revocation endpoint
            if (plugin.HasConfigForType<RevocationEndpoint>())
            {
                //Route revocation endpoint
                plugin.Route<RevocationEndpoint>();
            }

            int cacheLimit = oauth2Config["cache_size"].GetInt32();
            int maxTokensPerApp = oauth2Config["max_tokens_per_app"].GetInt32();
            int sessionIdSize = (int)oauth2Config["access_token_size"].GetUInt32();
            TimeSpan tokenValidFor = oauth2Config["token_valid_for_sec"].GetTimeSpan(TimeParseType.Seconds);
            TimeSpan cleanupInterval = oauth2Config["gc_interval_sec"].GetTimeSpan(TimeParseType.Seconds);
            string sessionIdPrefix = oauth2Config["cache_prefix"].GetString() ?? throw new KeyNotFoundException($"Missing required key 'cache_prefix' in '{OAUTH2_CONFIG_KEY}' config");

            //init the id provider
            OAuth2SessionIdProvider idProv = new(sessionIdPrefix, maxTokensPerApp, sessionIdSize, tokenValidFor);

            //Get shared global-cache
            IGlobalCacheProvider globalCache = plugin.GetGlobalCache(localized);

            //Create cache store from global cache
            GlobalCacheStore cacheStore = new(globalCache);

            //Init session provider now that client is loaded
            _sessions = new(cacheStore, cacheLimit, 100, idProv, plugin.GetContextOptions());

            //Schedule cleanup interval with the plugin scheduler
            plugin.ScheduleInterval(_sessions, cleanupInterval);

            //Wait and cleanup expired sessions
            _ = plugin.DeferTask(() => _sessions.CleanupExpiredSessionsAsync(localized, plugin.UnloadToken), 1000);

            localized.Information("Session provider loaded");

        }

        private async Task<IOAuth2TokenResult?> CreateTokenDelegateAsync(HttpEntity entity, UserApplication app, CancellationToken cancellation)
        {
            return await _sessions!.CreateAccessTokenAsync(entity, app, cancellation).ConfigureAwait(false);
        }
    }
}