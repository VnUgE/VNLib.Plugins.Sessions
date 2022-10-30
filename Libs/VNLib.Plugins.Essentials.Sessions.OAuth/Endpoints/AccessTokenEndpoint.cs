using System;
using System.Net;

using VNLib.Utils.Memory;
using VNLib.Plugins.Essentials.Oauth;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Extensions.Validation;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Sql;


namespace VNLib.Plugins.Essentials.Sessions.OAuth.Endpoints
{
    /// <summary>
    /// Grants authorization to OAuth2 clients to protected resources
    /// with access tokens
    /// </summary>
    internal sealed class AccessTokenEndpoint : ResourceEndpointBase
    {

        private readonly Lazy<ITokenManager> TokenStore;
        private readonly Applications Applications;

        //override protection settings to allow most connections to authenticate
        protected override ProtectionSettings EndpointProtectionSettings { get; } = new()
        {
            BrowsersOnly = false,
            SessionsRequired = false,
            VerifySessionCors = false
        };

        public AccessTokenEndpoint(string path, PluginBase pbase, Lazy<ITokenManager> tokenStore)
        {
            InitPathAndLog(path, pbase.Log);
            TokenStore = tokenStore;
            Applications = new(pbase.GetContextOptions(), pbase.GetPasswords());
        }

        protected override async ValueTask<VfReturnType> PostAsync(HttpEntity entity)
        {
            //Check for refresh token
            if (entity.RequestArgs.IsArgumentSet("grant_type", "refresh_token"))
            {
                //process a refresh token
            }
            //Check for grant_type parameter from the request body
            if (!entity.RequestArgs.IsArgumentSet("grant_type", "client_credentials"))
            {
                entity.CloseResponseError(HttpStatusCode.BadRequest, ErrorType.InvalidClient, "Invalid grant type");
                //Default to bad request
                return VfReturnType.VirtualSkip;
            }
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
                //Convert secret to private string
                PrivateString secretPv = new(secret, false);
                //Get the application from apps store
                UserApplication? app = await Applications.VerifyAppAsync(clientId, secretPv);
                if (app == null)
                {
                    //App was not found or the credentials do not match
                    entity.CloseResponseError(HttpStatusCode.UnprocessableEntity, ErrorType.InvalidClient, "The client credentials are invalid");
                    return VfReturnType.VirtualSkip;
                }
                //Create a new session
                IOAuth2TokenResult? result = await TokenStore.Value.CreateAccessTokenAsync(entity.Entity, app, entity.EventCancellation);
                if (result == null)
                {
                    entity.CloseResponseError(HttpStatusCode.ServiceUnavailable, ErrorType.TemporarilyUnabavailable, "You have reached the maximum number of valid tokens for this application");
                    return VfReturnType.VirtualSkip;
                }
                //Create the new response message
                OauthTokenResponseMessage tokenMessage = new()
                {
                    AccessToken = result.AccessToken,

                    //set expired as seconds in int form
                    Expires = result.ExpiresSeconds,
                    RefreshToken = result.RefreshToken,
                    TokenType = result.TokenType
                };
                //Respond with the token message
                entity.CloseResponseJson(HttpStatusCode.OK, tokenMessage);
                return VfReturnType.VirtualSkip;

            }
            //respond with error message
            entity.CloseResponseError(HttpStatusCode.UnprocessableEntity, ErrorType.InvalidClient, "The request was missing required arguments");
            return VfReturnType.VirtualSkip;
        }
    }
}