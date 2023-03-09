/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Sessions.VNCache
{

    public sealed class WebSessionProviderEntry : ISessionProvider
    {
        internal const string WEB_SESSION_CONFIG = "web";

        private WebSessionProvider? _sessions;
     

        //Web sessions can always be provided so long as cache is loaded
        public bool CanProcess(IHttpEvent entity) => _sessions != null && _sessions.IsConnected;

        ValueTask<SessionHandle> ISessionProvider.GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return _sessions!.GetSessionAsync(entity, cancellationToken);
        }


        public void Load(PluginBase plugin, ILogProvider localized)
        {
            //Load session provider
            _sessions = plugin.GetOrCreateSingleton<WebSessionProvider>();

            localized.Information("Session provider loaded");
        }
    }
}