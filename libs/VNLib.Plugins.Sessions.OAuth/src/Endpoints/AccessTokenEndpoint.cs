/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: AccessTokenEndpoint.cs 
*
* AccessTokenEndpoint.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
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
using System.Text.Json;
using System.Threading.Tasks;

using VNLib.Utils.Memory;
using VNLib.Hashing.IdentityUtility;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Oauth;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Oauth.Tokens;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Validation;

namespace VNLib.Plugins.Sessions.OAuth.Endpoints
{

    /// <summary>
    /// Grants authorization to OAuth2 clients to protected resources
    /// with access tokens
    /// </summary>
    [ConfigurationName(O2SessionProviderEntry.OAUTH2_CONFIG_KEY)]
    internal sealed class AccessTokenEndpoint : ResourceEndpointBase
    {
        private readonly IApplicationTokenFactory TokenFactory;
        private readonly ApplicationStore Applications;
        private readonly Task<ReadOnlyJsonWebKey?> JWTVerificationKey;

        //override protection settings to allow most connections to authenticate
        protected override ProtectionSettings EndpointProtectionSettings { get; } = new()
        {
            DisableBrowsersOnly = true,
            DisableSessionsRequired = true,
            DisableVerifySessionCors = true
        };

        public AccessTokenEndpoint(PluginBase pbase, IConfigScope config)
        {
            string? path = config["token_path"].GetString();;

            InitPathAndLog(path, pbase.Log);

            //Get the session provider, as its a token factory
            TokenFactory = pbase.GetOrCreateSingleton<OAuth2SessionProvider>();

            Applications = new(pbase.GetContextOptions(), pbase.GetPasswords());

            //Try to get the application token key for verifying signed application JWTs
            JWTVerificationKey = pbase.TryGetSecretAsync("application_token_key").ToJsonWebKey();
        }
       

        protected override async ValueTask<VfReturnType> PostAsync(HttpEntity entity)
        {
            //Check for refresh token
            if (entity.RequestArgs.IsArgumentSet("grant_type", "refresh_token"))
            {
                //process a refresh token
            }

            //See if we have an application authorized with JWT
            else if (entity.RequestArgs.IsArgumentSet("grant_type", "application"))
            {
                if(entity.RequestArgs.TryGetNonEmptyValue("token", out string? appJwt))
                {
                    //Try to get and verify the app
                    UserApplication? app = GetApplicationFromJwt(appJwt);
                    
                    //generate token
                    return await GenerateTokenAsync(entity, app);
                }
            }

            //Check for grant_type parameter from the request body
            else if (entity.RequestArgs.IsArgumentSet("grant_type", "client_credentials"))
            {
                //Get client id and secret (and make sure theyre not empty
                if (entity.RequestArgs.TryGetNonEmptyValue("client_id", out string? clientId) &&
                    entity.RequestArgs.TryGetNonEmptyValue("client_secret", out string? secret))
                {

                    if (!ValidatorExtensions.OnlyAlphaNumRegx.IsMatch(clientId))
                    {
                        //respond with error message
                        entity.CloseResponseError(HttpStatusCode.UnprocessableEntity, ErrorType.InvalidRequest, "Invalid client_id");
                        return VfReturnType.VirtualSkip;
                    }
                    if (!ValidatorExtensions.OnlyAlphaNumRegx.IsMatch(secret))
                    {
                        //respond with error message
                        entity.CloseResponseError(HttpStatusCode.UnprocessableEntity, ErrorType.InvalidRequest, "Invalid client_secret");
                        return VfReturnType.VirtualSkip;
                    }

                    //Convert the clientid and secret to lowercase
                    clientId = clientId.ToLower();
                    secret = secret.ToLower();

                    //Convert secret to private string that is unreferrenced
                    using PrivateString secretPv = new(secret, false);
                    
                    //Get the application from apps store
                    UserApplication? app = await Applications.VerifyAppAsync(clientId, secretPv);
                    
                    return await GenerateTokenAsync(entity, app);
                }
            }

            entity.CloseResponseError(HttpStatusCode.BadRequest, ErrorType.InvalidClient, "Invalid grant type");
            //Default to bad request
            return VfReturnType.VirtualSkip;
        }

        private UserApplication? GetApplicationFromJwt(string jwtData)
        {
            ReadOnlyJsonWebKey? verificationKey = JWTVerificationKey.GetAwaiter().GetResult();

            //Not enabled
            if (verificationKey == null)
            {
                return null;
            }

            //Parse application token
            using JsonWebToken jwt = JsonWebToken.Parse(jwtData);

            //verify the application jwt 
            if (!jwt.VerifyFromJwk(verificationKey))
            {
                return null;
            }

            using JsonDocument doc = jwt.GetPayload();

            //Get expiration time
            DateTimeOffset exp = doc.RootElement.GetProperty("exp").GetDateTimeOffset();

            //Check if token is expired
            return exp < DateTimeOffset.UtcNow ? null : UserApplication.FromJwtDoc(doc.RootElement);
        }


        private async Task<VfReturnType> GenerateTokenAsync(HttpEntity entity, UserApplication? app)
        {
            if (app == null)
            {
                //App was not found or the credentials do not match
                entity.CloseResponseError(HttpStatusCode.UnprocessableEntity, ErrorType.InvalidClient, "The credentials are invalid or do not exist");
                return VfReturnType.VirtualSkip;
            }

            IOAuth2TokenResult? result = await TokenFactory.CreateAccessTokenAsync(entity, app, entity.EventCancellation);
            
            if (result == null)
            {
                entity.CloseResponseError(HttpStatusCode.TooManyRequests, ErrorType.TemporarilyUnabavailable, "You have reached the maximum number of valid tokens for this application");
                return VfReturnType.VirtualSkip;
            }
            
            //Create the new response message
            OauthTokenResponseMessage tokenMessage = new()
            {
                AccessToken = result.AccessToken,
                IdToken = result.IdentityToken,
                //set expired as seconds in int form
                Expires = result.ExpiresSeconds,
                RefreshToken = result.RefreshToken,
                TokenType = result.TokenType
            };
            
            //Respond with the token message
            entity.CloseResponseJson(HttpStatusCode.OK, tokenMessage);
            return VfReturnType.VirtualSkip;
        }
    }
}