/*
* Copyright (c) 2024 Vaughn Nugent
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

/*
 * This file exists to make this library standalone. Meaining is can be loaded 
 * directly by the host as a plugin instead of being loaded by the session
 * provider plugin as an asset.
 */

using System;

using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Sessions.OAuth
{

    public sealed class O2AuthenticationPluginEntry : PluginBase
    {

        ///<inheritdoc/>
        public override string PluginName => "Essentials.Oauth.Authentication";

        ///<inheritdoc/>
        protected override void OnLoad()
        {
            OAuth2SessionProvider sessionProvider = this.GetOrCreateSingleton<OAuth2SessionProvider>();
            this.ExportService<ISessionProvider>(sessionProvider);

            Log.Information("Plugin loaded");
        }

        ///<inheritdoc/>
        protected override void OnUnLoad()
        {
            Log.Information("Plugin unloaded");
        }

        ///<inheritdoc/>
        protected override void ProcessHostCommand(string cmd)
        {
            throw new NotImplementedException();
        }
    }
}