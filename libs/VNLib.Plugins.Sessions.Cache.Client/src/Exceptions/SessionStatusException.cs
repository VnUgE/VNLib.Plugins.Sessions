/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: SessionStatusException.cs 
*
* SessionStatusException.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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


using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Sessions.Cache.Client.Exceptions
{
    /// <summary>
    /// Raised when the status of the session is invalid and cannot be used
    /// </summary>
    public class SessionStatusException : SessionException
    {
        public SessionStatusException()
        { }
        public SessionStatusException(string message) : base(message)
        { }
        public SessionStatusException(string message, Exception innerException) : base(message, innerException)
        { }
    }
}
