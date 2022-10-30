#nullable enable

using VNLib;

namespace VNLib.Plugins.Essentials.Sessions.OAuth
{
    public readonly struct TokenAndSessionIdResult
    {
        public readonly string SessionId;
        public readonly string AccessToken;
        public readonly string? RefreshToken;

        public TokenAndSessionIdResult(string sessionId, string token, string? refreshToken)
        {
            SessionId = sessionId;
            AccessToken = token;
            RefreshToken = refreshToken;
        }
    }
}
