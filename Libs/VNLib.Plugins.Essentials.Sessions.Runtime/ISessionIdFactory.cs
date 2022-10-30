using System.Diagnostics.CodeAnalysis;

using VNLib.Net.Http;

namespace VNLib.Plugins.Essentials.Sessions
{
    public interface ISessionIdFactory
    {
        /// <summary>
        /// Attempts to recover a session-id from the connection
        /// </summary>
        /// <param name="entity">The connection to process</param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        bool TryGetSessionId(IHttpEvent entity, [NotNullWhen(true)] out string? sessionId);
    }
}
