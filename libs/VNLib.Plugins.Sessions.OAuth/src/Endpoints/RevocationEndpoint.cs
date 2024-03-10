/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: RevocationEndpoint.cs 
*
* RevocationEndpoint.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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

using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Oauth;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Sessions.OAuth.Endpoints
{
    /// <summary>
    /// An OAuth2 authorized endpoint for revoking the access token
    /// held by the current connection
    /// </summary>
    [ConfigurationName("o2_revocation_endpoint")]
    internal class RevocationEndpoint : O2EndpointBase
    {

        public RevocationEndpoint(PluginBase pbase, IConfigScope config)
        {
            string? path = config.GetRequiredProperty("path", p => p.GetString()!);
            InitPathAndLog(path, pbase.Log);
        }

        protected override VfReturnType Post(HttpEntity entity)
        {
            //Revoke the access token, by invalidating it
            entity.Session.Invalidate();
            return VirtualOk(entity);
        }
    }
}
