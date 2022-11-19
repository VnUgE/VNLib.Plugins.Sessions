/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: SessionUpdateFailedException.cs 
*
* SessionUpdateFailedException.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Sessions.Cache.Client is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Sessions.Cache.Client is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Sessions.Cache.Client. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Runtime.Serialization;

namespace VNLib.Plugins.Sessions.Cache.Client.Exceptions
{
    public class SessionUpdateFailedException : SessionStatusException
    {
        public SessionUpdateFailedException()
        { }
        public SessionUpdateFailedException(string message) : base(message)
        { }
        public SessionUpdateFailedException(string message, Exception innerException) : base(message, innerException)
        { }
        protected SessionUpdateFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
        { }
    }
}
