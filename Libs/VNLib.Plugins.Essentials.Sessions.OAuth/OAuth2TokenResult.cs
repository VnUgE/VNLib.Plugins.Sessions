using VNLib.Plugins.Essentials.Oauth;

namespace VNLib.Plugins.Essentials.Sessions.OAuth
{
    internal class OAuth2TokenResult : IOAuth2TokenResult
    {
        public string? IdentityToken { get; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? TokenType { get; set; }
        public int ExpiresSeconds { get; set; }
    }
}
