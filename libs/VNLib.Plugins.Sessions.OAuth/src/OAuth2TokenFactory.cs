/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: OAuth2TokenFactory.cs 
*
* OAuth2TokenFactory.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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
using System.Net;
using System.Diagnostics.CodeAnalysis;

using VNLib.Hashing;
using VNLib.Net.Http;
using VNLib.Plugins.Sessions.Cache.Client;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Sessions.OAuth
{
    [ConfigurationName(OAuth2SessionProvider.OAUTH2_CONFIG_KEY)]
    internal sealed class OAuth2TokenFactory(PluginBase plugin, IConfigScope config) 
        : ISessionIdFactory, IOauthSessionIdFactory
    {
        private readonly OAuth2SessionConfig _config = config.DeserialzeAndValidate<OAuth2SessionConfig>();

        /*
         * ID Regeneration is always false as OAuth2 sessions 
         * do not allow dynamic ID updates, they require a 
         * negotiation
         */

        bool ISessionIdFactory.RegenerationSupported => false;

        /*
         * Connections that do not identify themselves, via a token are 
         * not valid. ID/Tokens must be created at once during 
         * authentication stage.
         */

        bool ISessionIdFactory.RegenIdOnEmptyEntry => false;


        ///<inheritdoc/>
        int IOauthSessionIdFactory.MaxTokensPerApp => _config.MaxTokensPerApp;

        ///<inheritdoc/>
        TimeSpan IOauthSessionIdFactory.SessionValidFor => TimeSpan.FromSeconds(_config.TokenLifeTimeSeconds);

        ///<inheritdoc/>
        string IOauthSessionIdFactory.TokenType => _config.TokenType;

        ///<inheritdoc/>
        bool ISessionIdFactory.CanService(IHttpEvent entity) => HasBearerToken(entity.Server, out _);

        ///<inheritdoc/>
        public GetTokenResult GenerateTokensAndId()
        {
            //Token is the raw value
            string token =  RandomHash.GetRandomBase64(_config.AccessTokenSize);

            //Return sessid result
            return new(token, null);
        }

        string ISessionIdFactory.RegenerateId(IHttpEvent entity)
        {
            throw new NotSupportedException("Id regeneration is not supported for OAuth2 sessions");
        }

        string? ISessionIdFactory.TryGetSessionId(IHttpEvent entity)
        {
            return HasBearerToken(entity.Server, out string ? token) ? token : null;
        }

        /// <summary>
        /// Gets the bearer token from an authorization header
        /// </summary>
        /// <param name="ci"></param>
        /// <param name="token">The token stored in the user's authorization header</param>
        /// <returns>True if the authorization header was set, has a Bearer token value</returns>
        private bool HasBearerToken(IConnectionInfo ci, [NotNullWhen(true)] out string? token)
        {
            //Get auth header value
            string? authorization = ci.Headers[HttpRequestHeader.Authorization];

            //Check if its set
            if (!string.IsNullOrWhiteSpace(authorization))
            {
                int bearerIndex = authorization.IndexOf(_config.TokenType, StringComparison.OrdinalIgnoreCase);

                //Check if the token type is present
                if (bearerIndex >= 0)
                {
                    //Calc token offset, get token, and trim any whitespace
                    token = authorization.AsSpan(bearerIndex + _config.TokenType.Length).Trim().ToString();
                    return true;
                }
            }

            token = null;
            return false;
        }
    }
}