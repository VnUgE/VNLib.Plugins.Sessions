/*
* Copyright (c) 2023 Vaughn Nugent
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

using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Plugins.Sessions.OAuth.Endpoints;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Routing;

namespace VNLib.Plugins.Sessions.OAuth
{

    public sealed class O2SessionProviderEntry : ISessionProvider
    {
        public const string OAUTH2_CONFIG_KEY = "oauth2";

        private OAuth2SessionProvider? _sessions;

        public bool CanProcess(IHttpEvent entity)
        {
            //If authorization header is set try to process as oauth2 session
            return _sessions != null && _sessions.IsConnected && entity.Server.Headers.HeaderSet(System.Net.HttpRequestHeader.Authorization);
        }

        ValueTask<SessionHandle> ISessionProvider.GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return _sessions!.GetSessionAsync(entity, cancellationToken);
        }      

        public void Load(PluginBase plugin, ILogProvider localized)
        {
            IConfigScope o2Config = plugin.GetConfig(OAUTH2_CONFIG_KEY);

            //Access token endpoint is optional
            if (o2Config.ContainsKey("token_path"))
            {
                //Create token endpoint
                plugin.Route<AccessTokenEndpoint>();
            }

            //Optional revocation endpoint
            if (plugin.HasConfigForType<RevocationEndpoint>())
            {
                //Route revocation endpoint
                plugin.Route<RevocationEndpoint>();
            }

            //Init session provider
            _sessions = plugin.GetOrCreateSingleton<OAuth2SessionProvider>();
            _sessions.SetLog(localized);

            localized.Information("Session provider loaded");
        }
    }
}