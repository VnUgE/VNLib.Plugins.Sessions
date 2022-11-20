/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.Memory
* File: SessionIdFactory.cs 
*
* SessionIdFactory.cs is part of VNLib.Plugins.Essentials.Sessions.Memory which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.Memory is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.Memory is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Diagnostics.CodeAnalysis;

using VNLib.Hashing;
using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Essentials.Sessions.Runtime;

#nullable enable

namespace VNLib.Plugins.Essentials.Sessions.Memory
{
    internal sealed class SessionIdFactory : ISessionIdFactory
    {
        private readonly int IdSize;
        private readonly string cookieName;
        private readonly TimeSpan ValidFor;

        public SessionIdFactory(uint idSize, string cookieName, TimeSpan validFor)
        {
            IdSize = (int)idSize;
            this.cookieName = cookieName;
            ValidFor = validFor;
        }

        public string GenerateSessionId(IHttpEvent entity)
        {
            //Random hex hash
            string cookie = RandomHash.GetRandomBase32(IdSize);

            //Set the session id cookie
            entity.Server.SetCookie(cookieName, cookie, ValidFor, secure: true, httpOnly: true);

            //return session-id value from cookie value
            return cookie;
        }
        
        public bool TryGetSessionId(IHttpEvent entity, [NotNullWhen(true)] out string? sessionId)
        {
            //Get authorization token and make sure its not too large to cause a buffer overflow
            if (entity.Server.GetCookie(cookieName, out sessionId))
            {
                return true;
            }
            //Only add sessions for user-agents
            else if (entity.Server.IsBrowser())
            {
                //Get a new session id
                sessionId = GenerateSessionId(entity);

                return true;
            }
            else
            {
                sessionId = null;
                return false;
            }
        }
    }
}
