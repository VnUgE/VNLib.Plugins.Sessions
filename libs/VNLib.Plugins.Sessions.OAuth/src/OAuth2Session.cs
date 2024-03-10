/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: OAuth2Session.cs 
*
* OAuth2Session.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Sessions.Cache.Client;

namespace VNLib.Plugins.Sessions.OAuth
{

    /// <summary>
    /// The implementation of the OAuth2 session container for HTTP sessions
    /// </summary>
    internal sealed class OAuth2Session(string sessionId, IDictionary<string, string> data, bool isNew) 
        : RemoteSession(sessionId, data, isNew)
    {
        public void InitNewSession(IHttpEvent entity)
        {
            SessionType = SessionType.OAuth2;
            Created = DateTimeOffset.UtcNow;
            //Set user-ip address
            UserIP = entity.Server.GetTrustedIp();
        }

        ///<inheritdoc/>
        ///<exception cref="NotSupportedException"></exception>
        public override string Token
        {
            get => throw new NotSupportedException("Token property is not supported for OAuth2 sessions");
            set => throw new NotSupportedException("Token property is not supported for OAuth2 sessions");
        }

        ///<inheritdoc/>
        protected override void IndexerSet(string key, string value)
        {
            //Guard protected entires
            switch (key)
            {
                case TOKEN_ENTRY:
                    throw new InvalidOperationException("Token entry may not be changed!");
            }
            base.IndexerSet(key, value);
        }
    }
}
