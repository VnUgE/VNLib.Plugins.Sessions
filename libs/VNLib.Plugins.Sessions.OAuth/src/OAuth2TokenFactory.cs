/*
* Copyright (c) 2023 Vaughn Nugent
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

using VNLib.Hashing;
using VNLib.Net.Http;
using VNLib.Plugins.Sessions.Cache.Client;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Essentials.Extensions;


namespace VNLib.Plugins.Sessions.OAuth
{
    [ConfigurationName(OAuth2SessionProvider.OAUTH2_CONFIG_KEY)]
    internal sealed class OAuth2TokenFactory : ISessionIdFactory, IOauthSessionIdFactory
    {
        private readonly OAuth2SessionConfig _config;

        public OAuth2TokenFactory(PluginBase plugin, IConfigScope config)
        {
            //Get the oauth2 config
            _config = config.DeserialzeAndValidate<OAuth2SessionConfig>();
        }

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
        string IOauthSessionIdFactory.TokenType => "Bearer";

        ///<inheritdoc/>
        bool ISessionIdFactory.CanService(IHttpEvent entity)
        {
            return entity.Server.HasAuthorization(out _);
        }

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
            return entity.Server.HasAuthorization(out string? token) ? token : null;
        }
    }
}