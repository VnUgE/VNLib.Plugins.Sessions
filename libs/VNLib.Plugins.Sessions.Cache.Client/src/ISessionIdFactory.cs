/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: ISessionIdFactory.cs 
*
* ISessionIdFactory.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

using VNLib.Net.Http;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// A factory that gets sessionIds from connections and regenerates
    /// session ids on connections if supported
    /// </summary>
    public interface ISessionIdFactory
    {
        /// <summary>
        /// A value that indicates if session id regeneration
        /// will be supported by this provider
        /// </summary>
        bool RegenerationSupported { get; }

        /// <summary>
        /// A value that indicates if a session id should be 
        /// regenerated when the remote cache does not have a 
        /// result for the recovered id
        /// </summary>
        /// <remarks>
        /// This is considered a security feature to dissalow clients
        /// from injecting thier own sessions
        /// </remarks>
        bool RegenIdOnEmptyEntry { get; }

        /// <summary>
        /// Indicates if the request can be serviced
        /// </summary>
        /// <param name="entity">The entity to service</param>
        /// <returns>True if a session id can be provided for this connection</returns>
        bool CanService(IHttpEvent entity);

        /// <summary>
        /// Regenerates the session id for the given connection
        /// </summary>
        /// <param name="entity">The connection to regenreate the id for</param>
        /// <returns>The new session id for the connection, or null if regeneration fails</returns>
        string RegenerateId(IHttpEvent entity);

        /// <summary>
        /// Attempts to recover a session id from the connection. If null is 
        /// returned it is consisdered a failure.
        /// </summary>
        /// <param name="entity">The connection to retrieve the sessionId for</param>
        /// <returns>The session id if successfull, null otherwise</returns>
        string? TryGetSessionId(IHttpEvent entity);
    }
}
