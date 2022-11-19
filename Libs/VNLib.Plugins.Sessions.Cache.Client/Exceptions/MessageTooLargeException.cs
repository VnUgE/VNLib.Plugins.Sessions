/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: MessageTooLargeException.cs 
*
* MessageTooLargeException.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

using VNLib.Net.Messaging.FBM;

namespace VNLib.Plugins.Sessions.Cache.Client.Exceptions
{
    /// <summary>
    /// Raised when a request message is too large to send to 
    /// the server and the server may close the connection.
    /// </summary>
    public class MessageTooLargeException : FBMException
    {
        ///<inheritdoc/>
        public MessageTooLargeException()
        { }
        ///<inheritdoc/>
        public MessageTooLargeException(string message) : base(message)
        { }
        ///<inheritdoc/>
        public MessageTooLargeException(string message, Exception innerException) : base(message, innerException)
        { }
        ///<inheritdoc/>
        protected MessageTooLargeException(SerializationInfo info, StreamingContext context) : base(info, context)
        { }
    }
}
