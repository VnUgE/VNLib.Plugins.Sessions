/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: WebSession.cs 
*
* WebSession.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Sessions.Cache.Client;


namespace VNLib.Plugins.Sessions.VNCache
{
    internal sealed class WebSession(string sessionId, IDictionary<string, string> sessionData, bool isNew)
        : RemoteSession(sessionId, sessionData, isNew)
    {
        internal void InitNewSession(IHttpEvent entity)
        {
            SessionType = SessionType.Web;
            Created = DateTimeOffset.UtcNow;
            //Set user-ip address
            UserIP = entity.Server.GetTrustedIp();

            /*
             * We do not need to set the IsModifed flag because the above statments 
             * should set it automatically 
             */
            //IsModified = true;
        }

        public override IDictionary<string, string> GetSessionData()
        {
            //Update variables before getting data
            if (Flags.IsSet(INVALID_MSK))
            {
                //Sessions that are invalid are destroyed and created later
            }
            else if (Flags.IsSet(REGEN_ID_MSK))
            {
                //Update created time
                Created = DateTimeOffset.UtcNow;
            }
            else if (Flags.IsSet(MODIFIED_MSK))
            {
                //Nothing needs to be done here, state will be preserved
            }

            return base.GetSessionData();
        }
    }
}
