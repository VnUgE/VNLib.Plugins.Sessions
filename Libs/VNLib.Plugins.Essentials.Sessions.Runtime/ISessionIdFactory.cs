/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.Runtime
* File: ISessionIdFactory.cs 
*
* ISessionIdFactory.cs is part of VNLib.Plugins.Essentials.Sessions.Runtime which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.Runtime is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.Runtime is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Diagnostics.CodeAnalysis;

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.Sessions.Runtime
{
    public interface ISessionIdFactory
    {
        /// <summary>
        /// Attempts to recover a session-id from the connection
        /// </summary>
        /// <param name="entity">The connection to process</param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        bool TryGetSessionId(IHttpEvent entity, [NotNullWhen(true)] out string? sessionId);
    }
}
