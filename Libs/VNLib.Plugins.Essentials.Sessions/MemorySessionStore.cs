using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Hashing;
using VNLib.Net.Http;
using VNLib.Net.Sessions;
using VNLib.Utils;
using VNLib.Utils.Async;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Extensions;


namespace VNLib.Plugins.Essentials.Sessions.Memory
{

    /// <summary>
    /// An <see cref="ISessionProvider"/> for in-process-memory backed sessions
    /// </summary>
    internal sealed class MemorySessionStore : ISessionProvider
    {
        private readonly Dictionary<string, MemorySession> SessionsStore;

        internal readonly MemorySessionConfig Config;

        public MemorySessionStore(MemorySessionConfig config)
        {
            Config = config;
            SessionsStore = new(config.MaxAllowedSessions, StringComparer.Ordinal);
        }
        
        ///<inheritdoc/>
        public async ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            
            static ValueTask SessionHandleClosedAsync(ISession session, IHttpEvent ev)
            {
                return (session as MemorySession).UpdateAndRelease(true, ev);
            }
            
            //Check for previous session cookie
            if (entity.Server.RequestCookies.TryGetNonEmptyValue(Config.SessionCookieID, out string sessionId))
            {
                //Try to get the old record or evict it
                ERRNO result = SessionsStore.TryGetOrEvictRecord(sessionId, out MemorySession session);
                if(result > 0)
                {
                    //Valid, now wait for exclusive access
                    await session.WaitOneAsync(cancellationToken);
                    return new (session, SessionHandleClosedAsync);
                }
                //Continue creating a new session
            }
            
            //Dont service non browsers for new sessions
            if (!entity.Server.IsBrowser())
            {
                return SessionHandle.Empty;
            }
            
            //try to cleanup expired records
            SessionsStore.CollectRecords();
            //Make sure there is enough room to add a new session
            if (SessionsStore.Count >= Config.MaxAllowedSessions)
            {
                entity.Server.SetNoCache();
                //Set 503 when full
                entity.CloseResponse(System.Net.HttpStatusCode.ServiceUnavailable);
                //Cannot service new session
                return new(null, FileProcessArgs.VirtualSkip, null);
            }
            //Initialze a new session
            MemorySession ms = new(entity.Server.GetTrustedIp(), this);
            //Set session cookie 
            SetSessionCookie(entity, ms);
            //Increment the semaphore
            (ms as IWaitHandle).WaitOne();
            //store the session in cache while holding semaphore, and set its expiration
            SessionsStore.StoreRecord(ms.SessionID, ms, Config.SessionTimeout);
            //Init new session handle
            return new SessionHandle(ms, SessionHandleClosedAsync);
        }

        /// <summary>
        /// Gets a new unique sessionid for sessions
        /// </summary>
        internal string NewSessionID => RandomHash.GetRandomHex((int)Config.SessionIdSizeBytes);

        internal void UpdateRecord(string newSessId, MemorySession session)
        {
            lock (SessionsStore)
            {
                //Remove old record from the store
                SessionsStore.Remove(session.SessionID);
                //Insert the new session
                SessionsStore.Add(newSessId, session);
            }
        }
        /// <summary>
        /// Sets a standard session cookie for an entity/connection
        /// </summary>
        /// <param name="entity">The entity to set the cookie on</param>
        /// <param name="session">The session attached to the </param>
        internal void SetSessionCookie(IHttpEvent entity, MemorySession session)
        {
            //Set session cookie 
            entity.Server.SetCookie(Config.SessionCookieID, session.SessionID, null, "/", Config.SessionTimeout, CookieSameSite.Lax, true, true);
        }
        /// <summary>
        /// Evicts all sessions from the current store
        /// </summary>
        public void Cleanup()
        {
            //Expire all old records to cleanup all entires
            this.SessionsStore.CollectRecords(DateTime.MaxValue);
        }
        /// <summary>
        /// Collects all expired records from the current store
        /// </summary>
        public void GC()
        {
            //collect expired records
            this.SessionsStore.CollectRecords();
        }
    }
}
