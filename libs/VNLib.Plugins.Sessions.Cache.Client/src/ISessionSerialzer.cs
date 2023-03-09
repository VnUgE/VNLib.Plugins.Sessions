/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: ISessionSerialzer.cs 
*
* ISessionSerialzer.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

using System.Diagnostics.CodeAnalysis;

using VNLib.Utils.Async;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// A specialized <see cref="IAsyncAccessSerializer{TMoniker}"/>
    /// that allows for re-using an instance that may be awaited
    /// </summary>
    /// <typeparam name="TSession">The session type</typeparam>
    public interface ISessionSerialzer<TSession> : IAsyncAccessSerializer<TSession>
    {
        /// <summary>
        /// Attempts to get an active session in the wait table as an atomic operation
        /// </summary>
        /// <param name="sessionId">The id of the session to retreive from the store</param>
        /// <param name="session">The stored session</param>
        /// <returns>A value that inidcates if the session was found</returns>
        bool TryGetSession(string sessionId, [NotNullWhen(true)] out TSession? session);
    }
}
