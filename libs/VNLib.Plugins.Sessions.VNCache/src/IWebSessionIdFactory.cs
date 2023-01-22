/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: IWebSessionIdFactory.cs 
*
* IWebSessionIdFactory.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
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
using System.Diagnostics.CodeAnalysis;

using VNLib.Net.Http;

namespace VNLib.Plugins.Sessions.VNCache
{
    /// <summary>
    /// Id factory for <see cref="WebSessionProvider"/>
    /// </summary>
    internal interface IWebSessionIdFactory
    {
        /// <summary>
        /// The maxium amount of time a session is valid for. Sessions will be invalidated
        /// after this time
        /// </summary>
        TimeSpan ValidFor { get; }

        /// <summary>
        /// Gets a new session-id for the connection and manipulates the entity as necessary
        /// </summary>
        /// <param name="entity">The connection to generate the new session for</param>
        /// <returns>The new session-id</returns>
        string GenerateSessionId(IHttpEvent entity);

        /// <summary>
        /// Attempts to recover a session id from 
        /// </summary>
        /// <param name="entity">The entity to get the session-id for</param>
        /// <param name="sessionId">The found ID for the session if accepted</param>
        /// <returns>True if a session id was found or set for the session</returns>
        bool TryGetSessionId(IHttpEvent entity, [NotNullWhen(true)] out string? sessionId);
    }    
}
