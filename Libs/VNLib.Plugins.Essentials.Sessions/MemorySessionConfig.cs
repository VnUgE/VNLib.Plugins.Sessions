using System;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.Sessions.Memory
{
    /// <summary>
    /// Represents configration variables used to create and operate http sessions. 
    /// </summary>
    public readonly struct MemorySessionConfig
    {
        /// <summary>
        /// The name of the cookie to use for matching sessions
        /// </summary>
        public string SessionCookieID { get; init; }
        /// <summary>
        /// The size (in bytes) of the genreated SessionIds
        /// </summary>
        public uint SessionIdSizeBytes { get; init; }
        /// <summary>
        /// The amount of time a session is valid (within the backing store)
        /// </summary>
        public TimeSpan SessionTimeout { get; init; }
        /// <summary>
        /// The log for which all errors within the <see cref="SessionProvider"/> instance will be written to. 
        /// </summary>
        public ILogProvider SessionLog { get; init; }
        /// <summary>
        /// The maximum number of sessions allowed to be cached in memory. If this value is exceed requests to this 
        /// server will be denied with a 503 error code
        /// </summary>
        public int MaxAllowedSessions { get; init; }
    }
}