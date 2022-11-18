using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Sessions.Runtime;

namespace VNLib.Plugins.Essentials.Sessions.VNCache
{
    /// <summary>
    /// Id factory for <see cref="WebSessionProvider"/>
    /// </summary>
    internal interface IWebSessionIdFactory: ISessionIdFactory
    {
        /// <summary>
        /// The maxium amount of time a session is valid for. Sessions will be invalidated
        /// after this time
        /// </summary>
        TimeSpan ValidFor { get; }

        /// <summary>
        /// Gets a new session-id for the connection and manipulates the entity as necessary
        /// </summary>
        /// <param name="entity">The connection to generate the new session for</param>
        /// <returns>The new session-id</returns>
        string GenerateSessionId(IHttpEvent entity);
    }

    
}
