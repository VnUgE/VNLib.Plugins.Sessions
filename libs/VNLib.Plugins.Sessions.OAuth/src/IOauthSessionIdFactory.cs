/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: IOauthSessionIdFactory.cs 
*
* IOauthSessionIdFactory.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.OAuth is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.OAuth is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;

namespace VNLib.Plugins.Sessions.OAuth
{
    internal interface IOauthSessionIdFactory 
    {
        /// <summary>
        /// The maxium number of tokens allowed to be created per OAuth application
        /// </summary>
        int MaxTokensPerApp { get; }

        /// <summary>
        /// The time a session is valid for
        /// </summary>
        TimeSpan SessionValidFor { get; }
        
        /// <summary>
        /// Called when the session provider wishes to generate a new session
        /// and required credential information to generate the new session
        /// </summary>
        /// <returns>The information genreated for the news ession</returns>
        GetTokenResult GenerateTokensAndId();
        
        /// <summary>
        /// The type of token this session provider generates
        /// </summary>
        string TokenType { get; }
    }
}
