/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: ISessionStore.cs 
*
* ISessionStore.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// A store for session types that attaches sessions to incomming http connections
    /// </summary>
    /// <typeparam name="TSession">The session type</typeparam>
    public interface ISessionStore<TSession> 
    {
        /// <summary>
        /// Gets a session for the given connection or returns null if no
        /// session could be attached to the connection
        /// </summary>
        /// <param name="entity">The connection to attach a session to</param>
        /// <param name="cancellationToken">A token to cancel that async operation</param>
        /// <returns>The session for the incomming connection, or null if no session was found</returns>
        ValueTask<TSession?> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken);

        /// <summary>
        /// Releases the session from the connection, as its no longer required. Cleanup tasks
        /// should be performed while the connection is still alive
        /// </summary>
        /// <param name="session">The session to detach</param>
        /// <param name="entity">The connection the session is attached to</param>
        /// <returns>A task that completes when the session has been detached</returns>
        /// <remarks>
        /// The connection results/request should not be modified. Cookies/headers
        /// may still be valid
        /// </remarks>
        ValueTask ReleaseSessionAsync(TSession session, IHttpEvent entity);
    }
}
