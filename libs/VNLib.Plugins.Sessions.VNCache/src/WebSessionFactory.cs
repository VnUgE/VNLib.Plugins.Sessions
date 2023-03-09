/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: WebSessionFactory.cs 
*
* WebSessionFactory.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
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
using VNLib.Plugins.Sessions.Cache.Client;


namespace VNLib.Plugins.Sessions.VNCache
{
    internal sealed class WebSessionFactory : ISessionFactory<WebSession>
    {
        private static IDictionary<string, string> GetDict() => new Dictionary<string, string>(10, StringComparer.OrdinalIgnoreCase);

        public WebSession GetNewSession(IHttpEvent connection, string sessionId, IDictionary<string, string>? sessionData)
        {
            /*
             * Create the new session and initialize it
             * If the initial data does not exist, create a new default dictionary 
             * the session is considered new if the session data was empty
             */
            WebSession ws = new(sessionId, sessionData ?? GetDict(), sessionData == null);

            if (ws.IsNew)
            {
                //init fresh session
                ws.InitNewSession(connection);
            }

            return ws;
        }
    }
}
