using System;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Oauth;
using VNLib.Plugins.Sessions.Cache.Client;

namespace VNLib.Plugins.Essentials.Sessions.OAuth
{
    public interface IOauthSessionIdFactory : ISessionIdFactory
    {
        /// <summary>
        /// The maxium number of tokens allowed to be created per OAuth application
        /// </summary>
        int MaxTokensPerApp { get; }
        /// <summary>
        /// Allows for custom configuration of the newly created session and 
        /// the <see cref="IHttpEvent"/> its attached to
        /// </summary>
        /// <param name="session">The newly created session</param>
        /// <param name="app">The application associated with the session</param>
        /// <param name="entity">The http event that generated the new session</param>
        void InitNewSession(RemoteSession session, UserApplication app, IHttpEvent entity);
        /// <summary>
        /// The time a session is valid for
        /// </summary>
        TimeSpan SessionValidFor { get; }
        /// <summary>
        /// Called when the session provider wishes to generate a new session
        /// and required credential information to generate the new session
        /// </summary>
        /// <returns>The information genreated for the news ession</returns>
        TokenAndSessionIdResult GenerateTokensAndId();
        /// <summary>
        /// The type of token this session provider generates
        /// </summary>
        string TokenType { get; }
    }
}
