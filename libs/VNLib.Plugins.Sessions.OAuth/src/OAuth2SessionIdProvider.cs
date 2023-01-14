/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: OAuth2SessionIdProvider.cs 
*
* OAuth2SessionIdProvider.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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
using System.Diagnostics.CodeAnalysis;

using VNLib.Hashing;
using VNLib.Net.Http;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Sessions.Cache.Client;
using static VNLib.Plugins.Essentials.Oauth.OauthSessionExtensions;

namespace VNLib.Plugins.Sessions.OAuth
{
    /// <summary>
    /// Generates secure OAuth2 session tokens and initalizes new OAuth2 sessions
    /// </summary>
    internal class OAuth2SessionIdProvider : IOauthSessionIdFactory
    {
        private readonly string SessionIdPrefix;
        private readonly int _bufferSize;
        private readonly int _tokenSize;

        ///<inheritdoc/>
        public int MaxTokensPerApp { get; }
        ///<inheritdoc/>
        public TimeSpan SessionValidFor { get; }
        ///<inheritdoc/>
        string IOauthSessionIdFactory.TokenType => "Bearer";

        public OAuth2SessionIdProvider(string sessionIdPrefix, int maxTokensPerApp, int tokenSize, TimeSpan validFor)
        {
            SessionIdPrefix = sessionIdPrefix;
            MaxTokensPerApp = maxTokensPerApp;
            SessionValidFor = validFor;
            _tokenSize = tokenSize;
            _bufferSize = tokenSize * 2;
        }
       
        ///<inheritdoc/>
        bool IOauthSessionIdFactory.TryGetSessionId(IHttpEvent entity, [NotNullWhen(true)] out string? sessionId)
        {
            //Get authorization token and make sure its not too large to cause a buffer overflow
            if (entity.Server.HasAuthorization(out string? token) && (token.Length + SessionIdPrefix.Length) <= _bufferSize)
            {
                //Compute session id from token
                sessionId = ComputeSessionIdFromToken(token);

                return true;
            }
            else
            {
                sessionId = null;
            }

            return false;
        }

        private string ComputeSessionIdFromToken(string token)
        {
            //Buffer to copy data to
            using UnsafeMemoryHandle<char> buffer = MemoryUtil.UnsafeAlloc<char>(_bufferSize, true);

            //Writer to accumulate data
            ForwardOnlyWriter<char> writer = new(buffer.Span);

            //Append session id prefix and token
            writer.Append(SessionIdPrefix);
            writer.Append(token);

            //Compute base64 hash of token and 
            return ManagedHash.ComputeBase64Hash(writer.AsSpan(), HashAlg.SHA256);
        }

        ///<inheritdoc/>
        TokenAndSessionIdResult IOauthSessionIdFactory.GenerateTokensAndId()
        {
            //Alloc buffer for random data
            using UnsafeMemoryHandle<byte> mem = MemoryUtil.UnsafeAlloc<byte>(_tokenSize, true);
            
            //Generate token from random cng bytes
            RandomHash.GetRandomBytes(mem);
            
            //Token is the raw value
            string token = Convert.ToBase64String(mem.Span);

            //The session id is the HMAC of the token
            string sessionId = ComputeSessionIdFromToken(token);
            
            //Clear buffer
            MemoryUtil.InitializeBlock(mem.Span);
            
            //Return sessid result
            return new(sessionId, token, null);
        }

        ///<inheritdoc/>
        void IOauthSessionIdFactory.InitNewSession(RemoteSession session, UserApplication app, IHttpEvent entity)
        {
            //Store session variables
            session[APP_ID_ENTRY] = app.Id;
            session[TOKEN_TYPE_ENTRY] = "client_credential,bearer";
            session[SCOPES_ENTRY] = app.Permissions;
            session.UserID = app.UserId;
        }
    }
}
