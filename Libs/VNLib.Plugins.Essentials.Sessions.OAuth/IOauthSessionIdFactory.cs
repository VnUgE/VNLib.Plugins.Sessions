/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: IOauthSessionIdFactory.cs 
*
* IOauthSessionIdFactory.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Essentials.Sessions.Runtime;
using VNLib.Plugins.Sessions.Cache.Client;

namespace VNLib.Plugins.Essentials.Sessions.OAuth
{
    public interface IOauthSessionIdFactory : ISessionIdFactory
    {
        /// <summary>
        /// The maxium number of tokens allowed to be created per OAuth application
        /// </summary>
        int MaxTokensPerApp { get; }
        /// <summary>
        /// Allows for custom configuration of the newly created session and 
        /// the <see cref="IHttpEvent"/> its attached to
        /// </summary>
        /// <param name="session">The newly created session</param>
        /// <param name="app">The application associated with the session</param>
        /// <param name="entity">The http event that generated the new session</param>
        void InitNewSession(RemoteSession session, UserApplication app, IHttpEvent entity);
        /// <summary>
        /// The time a session is valid for
        /// </summary>
        TimeSpan SessionValidFor { get; }
        /// <summary>
        /// Called when the session provider wishes to generate a new session
        /// and required credential information to generate the new session
        /// </summary>
        /// <returns>The information genreated for the news ession</returns>
        TokenAndSessionIdResult GenerateTokensAndId();
        /// <summary>
        /// The type of token this session provider generates
        /// </summary>
        string TokenType { get; }
    }
}
