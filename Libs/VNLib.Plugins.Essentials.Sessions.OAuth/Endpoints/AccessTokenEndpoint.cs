using System;
using System.Net;
using System.Text.Json;

using VNLib.Utils.Memory;
using VNLib.Hashing.IdentityUtility;
using VNLib.Plugins.Essentials.Oauth;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Oauth.Applications;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Sql;
using VNLib.Plugins.Extensions.Validation;

namespace VNLib.Plugins.Essentials.Sessions.OAuth.Endpoints
{
    delegate Task<IOAuth2TokenResult?> CreateTokenImpl(HttpEntity ev, UserApplication application, CancellationToken cancellation = default);

    /// <summary>
    /// Grants authorization to OAuth2 clients to protected resources
    /// with access tokens
    /// </summary>
    internal sealed class AccessTokenEndpoint : ResourceEndpointBase
    {

        private readonly CreateTokenImpl CreateToken;
        private readonly Applications Applications;

        private readonly Task<JsonDocument?> JWTVerificationKey;

        //override protection settings to allow most connections to authenticate
        protected override ProtectionSettings EndpointProtectionSettings { get; } = new()
        {
            BrowsersOnly = false,
            SessionsRequired = false,
            VerifySessionCors = false
        };

        public AccessTokenEndpoint(string path, PluginBase pbase, CreateTokenImpl tokenStore, Task<JsonDocument?> verificationKey)
        {
            InitPathAndLog(path, pbase.Log);
            CreateToken = tokenStore;
            Applications = new(pbase.GetContextOptions(), pbase.GetPasswords());
            JWTVerificationKey = verificationKey;
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
                    PrivateString secretPv = new(secret, false);
                    
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
            //Not enabled
            if (JWTVerificationKey.Result == null)
            {
                return null;
            }

            //Parse application token
            using JsonWebToken jwt = JsonWebToken.Parse(jwtData);

            //verify the application jwt 
            if (!jwt.VerifyFromJwk(JWTVerificationKey.Result.RootElement))
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

            IOAuth2TokenResult? result = await CreateToken(entity, app, entity.EventCancellation);
            
            if (result == null)
            {
                entity.CloseResponseError(HttpStatusCode.ServiceUnavailable, ErrorType.TemporarilyUnabavailable, "You have reached the maximum number of valid tokens for this application");
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