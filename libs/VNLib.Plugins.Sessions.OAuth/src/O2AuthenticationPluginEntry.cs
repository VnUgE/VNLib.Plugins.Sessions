/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: O2AuthenticationPluginEntry.cs 
*
* O2AuthenticationPluginEntry.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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

using System.ComponentModel.Design;

using VNLib.Utils.Logging;
using VNLib.Plugins.Attributes;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Sessions.OAuth
{
    public sealed class O2AuthenticationPluginEntry : PluginBase
    {
        public override string PluginName => "Essentials.Oauth.Authentication";

        private OAuth2SessionProvider? SessionProvider;


        [ServiceConfigurator]
        public void OnServicesLoading(IServiceContainer services)
        {
            //Expose the OAuth2 session provider as a service singleton
            services.AddService<ISessionProvider>(SessionProvider!);
        }

        protected override void OnLoad()
        {
            SessionProvider = this.GetOrCreateSingleton<OAuth2SessionProvider>();
            Log.Information("Plugin loaded");
        }

        protected override void OnUnLoad()
        {
            Log.Information("Plugin unloaded");
        }

        protected override void ProcessHostCommand(string cmd)
        {
            throw new NotImplementedException();
        }
    }
}