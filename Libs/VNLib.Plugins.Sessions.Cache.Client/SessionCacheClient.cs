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
using VNLib.Utils;
using VNLib.Utils.Memory.Caching;
using VNLib.Net.Messaging.FBM.Client;
using VNLib.Plugins.Essentials.Sessions;

#nullable enable

namespace VNLib.Plugins.Sessions.Cache.Client
{

    /// <summary>
    /// A client that allows access to sessions located on external servers
    /// </summary>
    public abstract class SessionCacheClient : VnDisposeable, ICacheHolder
    {
        public class LRUSessionStore<T> : LRUCache<string, T> where T : ISession, ICacheable
        {
            public override bool IsReadOnly => false;
            protected override int MaxCapacity { get; }

            public LRUSessionStore(int maxCapacity) : base(StringComparer.Ordinal) => MaxCapacity = maxCapacity;

            protected override bool CacheMiss(string key, [NotNullWhen(true)] out T? value)
            {
                value = default;
                return false;
            }
            protected override void Evicted(KeyValuePair<string, T> evicted)
            {
                //Evice record
                evicted.Value.Evicted();
            }
        }

        protected readonly LRUSessionStore<RemoteSession> CacheTable;
        protected readonly object CacheLock;
        protected readonly int MaxLoadedEntires;

        protected FBMClient Client { get; }

        /// <summary>
        /// Initializes a new <see cref="SessionCacheClient"/>
        /// </summary>
        /// <param name="client"></param>
        /// <param name="maxCacheItems">The maximum number of sessions to keep in memory</param>
        protected SessionCacheClient(FBMClient client, int maxCacheItems)
        {
            MaxLoadedEntires = maxCacheItems;
            CacheLock = new();
            CacheTable = new(maxCacheItems);
            Client = client;
            //Listen for close events
            Client.ConnectionClosed += Client_ConnectionClosed;
        }

        private void Client_ConnectionClosed(object? sender, EventArgs e) => CacheHardClear();

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
            Check();
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
                CacheTable.Clear();
                foreach (RemoteSession session in (IEnumerable<RemoteSession>)CacheTable)
                {
                    session.Evicted();
                }
                CacheTable.Clear();
            }
        }

        protected override void Free()
        {
            //Unsub from events
            Client.ConnectionClosed -= Client_ConnectionClosed;
            //Clear all cached sessions
            CacheHardClear();
        }
    }
}
