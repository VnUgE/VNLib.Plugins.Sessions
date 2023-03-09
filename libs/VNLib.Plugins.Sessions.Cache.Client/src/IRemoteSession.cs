/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: IRemoteSession.cs 
*
* IRemoteSession.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

using System;
using System.Collections.Generic;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// A session that is expected to be stored in a remote caching storage system
    /// </summary>
    public interface IRemoteSession
    {
        /// <summary>
        /// Gets the ID of the session
        /// </summary>
        string SessionID { get; }

        /// <summary>
        /// Gets the status of the session during a release operation
        /// </summary>
        /// <returns>The <see cref="SessionStatus"/> flags that represent the status of the session</returns>
        SessionStatus GetStatus();

        /// <summary>
        /// Gets the internal session data to update when requested
        /// </summary>
        /// <returns>The internal session data</returns>
        IDictionary<string, string> GetSessionData();

        /// <summary>
        /// Destroys the internal state of the session so it cannot be 
        /// reused
        /// </summary>
        /// <param name="cause">A optional exception that caused the error condition</param>
        void Destroy(Exception? cause);

        /// <summary>
        /// Determines if the state of the session is valid for reuse by a waiting 
        /// connection. Optionally returns an exception that caused
        /// </summary>
        /// <param name="cause">The exception that caused the session state to transition to invalid</param>
        /// <returns>True of the session is valid, false if it cannot be reused</returns>
        bool IsValid(out Exception? cause);

        /// <summary>
        /// Called by the store to notify the session that a pending update has completed
        /// successfully.
        /// </summary>
        void SessionUpdateComplete();
    }
}
