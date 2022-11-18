using System.Diagnostics.CodeAnalysis;

using VNLib.Hashing;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Sessions.Runtime;

namespace VNLib.Plugins.Essentials.Sessions.VNCache
{
    /// <summary>
    /// <see cref="IWebSessionIdFactory"/> implementation, using 
    /// http cookies as session id storage
    /// </summary>
    internal sealed class WebSessionIdFactoryImpl : IWebSessionIdFactory
    {
        public TimeSpan ValidFor { get; }

        public string GenerateSessionId(IHttpEvent entity)
        {
            //Random hex hash
            string cookie = RandomHash.GetRandomBase32(_tokenSize);
          
            //Set the session id cookie
            entity.Server.SetCookie(SessionCookieName, cookie, ValidFor, secure: true, httpOnly: true);

            //return session-id value from cookie value
            return ComputeSessionIdFromCookie(cookie);
        }

        bool ISessionIdFactory.TryGetSessionId(IHttpEvent entity, [NotNullWhen(true)] out string? sessionId)
        {
            //Get authorization token and make sure its not too large to cause a buffer overflow
            if (entity.Server.GetCookie(SessionCookieName, out string? cookie) && (cookie.Length + SessionIdPrefix.Length) <= _bufferSize)
            {
                //Compute session id from token
                sessionId = ComputeSessionIdFromCookie(cookie);

                return true;
            }
            //Only add sessions for user-agents
            else if(entity.Server.IsBrowser())
            {
                //Get a new session id
                sessionId = GenerateSessionId(entity);

                return true;
            }
            else
            {
                sessionId = null;
                return false;
            }
        }

        private readonly string SessionCookieName;
        private readonly string SessionIdPrefix;
        private readonly int _bufferSize;
        private readonly int _tokenSize;

        /// <summary>
        /// Initialzies a new web session Id factory
        /// </summary>
        /// <param name="cookieSize">The size of the cookie in bytes</param>
        /// <param name="sessionCookieName">The name of the session cookie</param>
        /// <param name="sessionIdPrefix">The session-id internal prefix</param>
        /// <param name="validFor">The time the session cookie is valid for</param>
        public WebSessionIdFactoryImpl(uint cookieSize, string sessionCookieName, string sessionIdPrefix, TimeSpan validFor)
        {
            ValidFor = validFor;
            SessionCookieName = sessionCookieName;
            SessionIdPrefix = sessionIdPrefix;
            _tokenSize = (int)cookieSize;
            //Calc buffer size
            _bufferSize = Math.Max(32, ((int)cookieSize * 3) + sessionIdPrefix.Length);
        }
     

        private string ComputeSessionIdFromCookie(string sessionId)
        {
            //Buffer to copy data to
            using UnsafeMemoryHandle<char> buffer = Memory.UnsafeAlloc<char>(_bufferSize, true);

            //Writer to accumulate data
            ForwardOnlyWriter<char> writer = new(buffer.Span);

            //Append prefix and session id
            writer.Append(SessionIdPrefix);
            writer.Append(sessionId);

            //Compute base64 hash of token and 
            return ManagedHash.ComputeBase64Hash(writer.AsSpan(), HashAlg.SHA256);
        }
    }
}