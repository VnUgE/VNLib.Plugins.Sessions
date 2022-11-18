using System;
using System.Text.Json;

using VNLib.Plugins.Essentials.Oauth;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Essentials.Sessions.OAuth.Endpoints
{
    /// <summary>
    /// An OAuth2 authorized endpoint for revoking the access token
    /// held by the current connection
    /// </summary>
    [ConfigurationName("oauth2")]
    internal class RevocationEndpoint : O2EndpointBase
    {

        public RevocationEndpoint(PluginBase pbase, IReadOnlyDictionary<string, JsonElement> config)
        {
            string? path = config["revocation_path"].GetString();
            InitPathAndLog(path, pbase.Log);
        }

        protected override VfReturnType Post(HttpEntity entity)
        {
            //Revoke the access token, by invalidating it
            entity.Session.Invalidate();
            entity.CloseResponse(System.Net.HttpStatusCode.OK);
            return VfReturnType.VirtualSkip;
        }
    }
}
