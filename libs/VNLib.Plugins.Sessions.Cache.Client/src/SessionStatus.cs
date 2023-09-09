/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: SessionStatus.cs 
*
* SessionStatus.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// Flags for determining the status of an HTTP session
    /// before it is releases/post processed
    /// </summary>
    [Flags]
    public enum SessionStatus
    {
        /// <summary>
        /// The session has not been modified and does not need attention
        /// </summary>
        None = 0,
        /// <summary>
        /// The session is no longer valid and should be deleted
        /// </summary>
        Delete = 1,
        /// <summary>
        /// The session has been modified and requires its data be published
        /// </summary>
        UpdateOnly = 2,
        /// <summary>
        /// The session has been modified and requires an ID change
        /// </summary>
        RegenId = 4,
        /// <summary>
        /// The session should be detached from the current context
        /// </summary>
        Detach = 8,
    }
}
