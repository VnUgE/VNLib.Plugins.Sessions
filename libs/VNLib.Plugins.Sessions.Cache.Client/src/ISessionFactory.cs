/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: ISessionFactory.cs 
*
* ISessionFactory.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Sessions.Cache.Client is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Sessions.Cache.Client is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Collections.Generic;

using VNLib.Net.Http;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// A session factory that generates new sessions on demand by a <see cref="ISessionStore{TSession}"/>
    /// </summary>
    /// <typeparam name="TSession">The session type to generate</typeparam>
    public interface ISessionFactory<TSession>
    {
        /// <summary>
        /// Constructs a new session of the given type from the session Id 
        /// and its initial object data
        /// </summary>
        /// <param name="sessionId">The is of the session to create</param>
        /// <param name="sessionData">The initial session data to create the session from</param>
        /// <param name="entity">The connection to get the session for</param>
        /// <returns>The new session</returns>
        TSession? GetNewSession(IHttpEvent entity, string sessionId, IDictionary<string, string>? sessionData);
    }
}
