/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.Memory
* File: MemorySessionConfig.cs 
*
* MemorySessionConfig.cs is part of VNLib.Plugins.Essentials.Sessions.Memory which is part of the larger 
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

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.Sessions.Memory
{
    /// <summary>
    /// Represents configration variables used to create and operate http sessions. 
    /// </summary>
    public readonly struct MemorySessionConfig
    {
        /// <summary>
        /// The name of the cookie to use for matching sessions
        /// </summary>
        public string SessionCookieID { get; init; }
        /// <summary>
        /// The size (in bytes) of the genreated SessionIds
        /// </summary>
        public uint SessionIdSizeBytes { get; init; }
        /// <summary>
        /// The amount of time a session is valid (within the backing store)
        /// </summary>
        public TimeSpan SessionTimeout { get; init; }
        /// <summary>
        /// The log for which all errors within the <see cref="SessionProvider"/> instance will be written to. 
        /// </summary>
        public ILogProvider SessionLog { get; init; }
        /// <summary>
        /// The maximum number of sessions allowed to be cached in memory. If this value is exceed requests to this 
        /// server will be denied with a 503 error code
        /// </summary>
        public int MaxAllowedSessions { get; init; }
    }
}