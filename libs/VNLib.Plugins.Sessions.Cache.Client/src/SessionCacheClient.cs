/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: SessionCacheClient.cs 
*
* SessionCacheClient.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Sessions.Cache.Client is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Sessions.Cache.Client is distributed in the hope that it will be useful,
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
using System.Diagnostics.CodeAnalysis;

using VNLib.Net.Http;
using VNLib.Utils.Async;
using VNLib.Utils.Logging;
using VNLib.Utils.Memory.Caching;
using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Sessions.Cache.Client
{

    /// <summary>
    /// A client that allows access to sessions located on external servers
    /// </summary>
    public abstract class SessionCacheClient : ICacheHolder
    {
        public class LRUSessionStore<T> : LRUCache<string, T>, ICacheHolder where T : ISession
        {
            internal AsyncQueue<T> ExpiredSessions { get; }

            ///<inheritdoc/>
            public override bool IsReadOnly => false;
            ///<inheritdoc/>
            protected override int MaxCapacity { get; }

            
            public LRUSessionStore(int maxCapacity) : base(StringComparer.Ordinal)
            {
                MaxCapacity = maxCapacity;
                ExpiredSessions = new (true, true);
            }
            
            ///<inheritdoc/>
            protected override bool CacheMiss(string key, [NotNullWhen(true)] out T? value)
            {
                value = default;
                return false;
            }
            
            ///<inheritdoc/>
            protected override void Evicted(ref KeyValuePair<string, T> evicted)
            {
                //add to queue, the list lock should be held during this operatio
                _ = ExpiredSessions.TryEnque(evicted.Value);
            }

            ///<inheritdoc/>
            public void CacheClear()
            {
                foreach (KeyValuePair<string, T> value in List)
                {
                    KeyValuePair<string, T> onStack = value;

                    Evicted(ref onStack);
                }

                Clear();
            }

            ///<inheritdoc/>
            public void CacheHardClear()
            {
                CacheClear();
            }
        }

        protected readonly LRUSessionStore<RemoteSession> CacheTable;
        protected readonly object CacheLock;
        protected readonly int MaxLoadedEntires;

        /// <summary>
        /// The client used to communicate with the cache server
        /// </summary>
        protected IRemoteCacheStore Store { get; }

        /// <summary>
        /// Gets a value that determines if the backing <see cref="IRemoteCacheStore"/> is connected
        /// to a server
        /// </summary>
        public bool IsConnected => Store.IsConnected;

        /// <summary>
        /// Initializes a new <see cref="SessionCacheClient"/>
        /// </summary>
        /// <param name="client"></param>
        /// <param name="maxCacheItems">The maximum number of sessions to keep in memory</param>
        protected SessionCacheClient(IRemoteCacheStore client, int maxCacheItems)
        {
            MaxLoadedEntires = maxCacheItems;
            CacheLock = new();
            CacheTable = new(maxCacheItems);
            Store = client;
        }

        private ulong _waitingCount;

        /// <summary>
        /// The number of pending connections waiting for results from the cache server
        /// </summary>
        public ulong WaitingConnections => _waitingCount;

        /// <summary>
        /// Attempts to get a session from the cache identified by its sessionId asynchronously
        /// </summary>
        /// <param name="entity">The connection/request to attach the session to</param>
        /// <param name="sessionId">The ID of the session to retrieve</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A <see cref="ValueTask"/> that resolves the remote session</returns>
        /// <exception cref="SessionException"></exception>
        public virtual async ValueTask<RemoteSession> GetSessionAsync(IHttpEvent entity, string sessionId, CancellationToken cancellationToken)
        {
            try
            {
                RemoteSession? session;
                //Aquire lock on cache
                lock (CacheLock)
                {
                    //See if session is loaded into cache
                    if (!CacheTable.TryGetValue(sessionId, out session))
                    {
                        //Init new record
                        session = SessionCtor(sessionId);
                        //Add to cache
                        CacheTable.Add(session.SessionID, session);
                    }
                    //Valid entry found in cache
                }
                
                //Inc waiting count
                Interlocked.Increment(ref _waitingCount);

                try
                {
                    //Load session-data
                    await session.WaitAndLoadAsync(entity, cancellationToken);
                    return session;
                }
                catch
                {
                    //Remove the invalid cached session
                    lock (CacheLock)
                    {
                        _ = CacheTable.Remove(sessionId);
                    }
                    throw;
                }
                finally
                {
                    //Dec waiting count
                    Interlocked.Decrement(ref _waitingCount);
                }
            }
            catch (SessionException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            //Wrap exceptions
            catch (Exception ex)
            {
                throw new SessionException("An unhandled exception was raised", ex);
            }
        }

        /// <summary>
        /// Gets a new <see cref="RemoteSession"/> instances for the given sessionId,
        /// and places it a the head of internal cache
        /// </summary>
        /// <param name="sessionId">The session identifier</param>
        /// <returns>The new session for the given ID</returns>
        protected abstract RemoteSession SessionCtor(string sessionId);
        
        /// <summary>
        /// Begins waiting for expired sessions to be evicted from the cache table that 
        /// may have pending synchronization operations
        /// </summary>
        /// <param name="log"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task CleanupExpiredSessionsAsync(ILogProvider log, CancellationToken token)
        {
            while (true)
            {
                try
                {
                    //Wait for expired session and dispose it
                    using RemoteSession session = await CacheTable.ExpiredSessions.DequeueAsync(token);
                    
                    //Obtain lock on session
                    await session.WaitOneAsync(CancellationToken.None);

                    log.Verbose("Removed expired session {id}", session.SessionID);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                }
            }
        }

        ///<inheritdoc/>
        public void CacheClear()
        {

        }
        ///<inheritdoc/>
        public void CacheHardClear()
        {
            //Cleanup cache when disconnected
            lock (CacheLock)
            {
                CacheTable.CacheHardClear();
            }
        }
    }
}
