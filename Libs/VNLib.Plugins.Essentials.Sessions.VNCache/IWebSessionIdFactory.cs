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
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.VNCache is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Essentials.Sessions.VNCache. If not, see http://www.gnu.org/licenses/.
*/

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Sessions.Runtime;

namespace VNLib.Plugins.Essentials.Sessions.VNCache
{
    /// <summary>
    /// Id factory for <see cref="WebSessionProvider"/>
    /// </summary>
    internal interface IWebSessionIdFactory: ISessionIdFactory
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
    }

    
}
