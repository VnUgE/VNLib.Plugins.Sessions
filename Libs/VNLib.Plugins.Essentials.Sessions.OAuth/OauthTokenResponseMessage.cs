using System.Text.Json.Serialization;

#nullable enable

namespace VNLib.Plugins.Essentials.Sessions.OAuth
{
    public sealed class OauthTokenResponseMessage
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
        [JsonPropertyName("expires_in")]
        public int Expires { get; set; }
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }
}