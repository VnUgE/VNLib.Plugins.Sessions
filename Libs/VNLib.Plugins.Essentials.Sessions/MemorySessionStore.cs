/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.Memory
* File: MemorySessionStore.cs 
*
* MemorySessionStore.cs is part of VNLib.Plugins.Essentials.Sessions.Memory which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.Memory is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.Memory is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Net.Http.Core;
using VNLib.Utils;
using VNLib.Utils.Async;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Extensions;

#nullable enable

namespace VNLib.Plugins.Essentials.Sessions.Memory
{
    /// <summary>
    /// An <see cref="ISessionProvider"/> for in-process-memory backed sessions
    /// </summary>
    internal sealed class MemorySessionStore : ISessionProvider
    {
        private readonly Dictionary<string, MemorySession> SessionsStore;

        internal readonly MemorySessionConfig Config;
        internal readonly SessionIdFactory IdFactory;
        internal readonly AsyncQueue<MemorySession> ExpiredSessions;

        public MemorySessionStore(MemorySessionConfig config)
        {
            Config = config;
            SessionsStore = new(config.MaxAllowedSessions, StringComparer.Ordinal);
            IdFactory = new(config.SessionIdSizeBytes, config.SessionCookieID, config.SessionTimeout);
            ExpiredSessions = new(false, true);
        }
        
        ///<inheritdoc/>
        public async ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            
            static ValueTask SessionHandleClosedAsync(ISession session, IHttpEvent ev)
            {
                return (session as MemorySession)!.UpdateAndRelease(true, ev);
            }
            
            //Try to get the id for the session
            if (IdFactory.TryGetSessionId(entity, out string? sessionId))
            {
                //Try to get the old record or evict it
                ERRNO result = SessionsStore.TryGetOrEvictRecord(sessionId, out MemorySession? session);
                if(result > 0)
                {
                    //Valid, now wait for exclusive access
                    await session.WaitOneAsync(cancellationToken);
                    return new (session, SessionHandleClosedAsync);
                }
                else
                {
                    //try to cleanup expired records
                    GC();
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
                    session = new(sessionId, entity.Server.GetTrustedIp(), UpdateSessionId, ExpiredSessions);
                    //Increment the semaphore
                    (session as IWaitHandle).WaitOne();
                    //store the session in cache while holding semaphore, and set its expiration
                    SessionsStore.StoreRecord(session.SessionID, session, Config.SessionTimeout);
                    //Init new session handle
                    return new (session, SessionHandleClosedAsync);
                }
            }
            else
            {
                return SessionHandle.Empty;
            }
        }

        public async Task CleanupExiredAsync(ILogProvider log, CancellationToken token)
        {
            while (true)
            {
                try
                {
                    //Wait for expired session and dispose it
                    using MemorySession session = await ExpiredSessions.DequeueAsync(token);

                    //Obtain lock on session
                    await session.WaitOneAsync(CancellationToken.None);

                    log.Verbose("Removed expired session {id}", session.SessionID);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
        }

        private string UpdateSessionId(IHttpEvent entity, string oldId)
        {
            //Generate and set a new sessionid
            string newid = IdFactory.GenerateSessionId(entity);
            //Aquire lock on cache
            lock (SessionsStore)
            {
                //Change the cache lookup id
                if (SessionsStore.Remove(oldId, out MemorySession? session))
                {
                    SessionsStore.Add(newid, session);
                }
            }
            return newid;
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
