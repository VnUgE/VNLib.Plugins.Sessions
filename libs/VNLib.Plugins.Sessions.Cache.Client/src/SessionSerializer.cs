/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: SessionSerializer.cs 
*
* SessionSerializer.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

using VNLib.Utils.Memory.Caching;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// Concrete <see cref="ISessionSerialzer{TSession}"/> that provides 
    /// access serialization for session types
    /// </summary>
    /// <typeparam name="TSession">The session type</typeparam>
    public class SessionSerializer<TSession> : ISessionSerialzer<TSession>, ICacheHolder where TSession : IRemoteSession
    {

        private readonly object StoreLock;
        private readonly Stack<WaitEntry> _entryPool;
        private readonly Dictionary<string, WaitEntry> _waitStore;
        private readonly int MaxPoolSize;

        /// <summary>
        /// Initializes a new <see cref="SessionSerializer{TSession}"/>
        /// </summary>
        /// <param name="poolCapacity">The maximum number of <see cref="WaitEntry"/> elements to pool</param>
        public SessionSerializer(int poolCapacity)
        {
            StoreLock = new();
            _entryPool = new Stack<WaitEntry>(poolCapacity);
            
            //Session-ids are security senstive, we must use ordinal(binary) string comparison
            _waitStore = new Dictionary<string, WaitEntry>(poolCapacity, StringComparer.Ordinal);
            
            MaxPoolSize = poolCapacity;
        }

        ///<inheritdoc/>
        public virtual bool TryGetSession(string sessionId, [NotNullWhen(true)] out TSession? session)
        {
            lock (StoreLock)
            {
                //Try to see if an entry is loaded, and get the session
                bool result = _waitStore.TryGetValue(sessionId, out WaitEntry? entry);
                session = result ? entry!.Session : default;
                return result;
            }
        }

        ///<inheritdoc/>
        public virtual Task WaitAsync(TSession moniker, CancellationToken cancellation = default)
        {
            //Token must not be cancelled 
            cancellation.ThrowIfCancellationRequested();

            WaitEnterToken token;

            lock (StoreLock)
            {
                //See if the entry already exists, otherwise get a new wait entry
                if (!_waitStore.TryGetValue(moniker.SessionID, out WaitEntry? wait))
                {
                    GetWaitEntry(ref wait, moniker);

                    //Add entry to store
                    _waitStore[moniker.SessionID] = wait;
                }

                //Get waiter before leaving lock
                token = wait.GetWaiter();
            }

            return token.EnterWaitAsync(cancellation);
        }

        ///<inheritdoc/>
        public virtual void Release(TSession moniker)
        {
            /*
             * When releasing a lock on a moniker, we store entires in an internal table. Wait entires also require mutual
             * exclustion to properly track waiters. This happens inside a single lock for lower entry times/complexity. 
             * The wait's internal semaphore may also cause longer waits within the lock, so wait entires are "prepared"
             * by using tokens to access the wait/release mechanisms with proper tracking.
             * 
             * Tokens can be used to control the wait because the call to release may cause thread yielding (if internal 
             * WaitHandle is being used), so we don't want to block other callers.
             * 
             * When there are no more waiters for a moniker at the time the lock was entered, the WaitEntry is released
             * back to the pool.
             */
            
            WaitReleaseToken releaser;

            lock (StoreLock)
            {
                WaitEntry entry = _waitStore[moniker.SessionID];
                
                //Call release while holding store lock
                if(entry.Release(out releaser) == 0)
                {
                    //No more waiters
                    _waitStore.Remove(moniker.SessionID);
                    
                    /*
                     * We must release the semaphore before returning to pool, 
                     * its safe because there are no more waiters
                     */
                    releaser.Release();

                    ReturnEntry(entry);

                    //already released
                    releaser = default;
                }
            }
            //Release sem outside of lock
            releaser.Release();
        }

        private void GetWaitEntry([NotNull] ref WaitEntry? wait, TSession session)
        {
            //Try to get wait from pool
            if(!_entryPool.TryPop(out wait))
            {
                wait = new();
            }

            //Init wait with session
            wait.Prepare(session);
        }

        private void ReturnEntry(WaitEntry entry)
        {
            //Remove session ref
            entry.Prepare(default);
            
            if(_entryPool.Count < MaxPoolSize)
            {
                _entryPool.Push(entry);
            }
            else
            {
                //Dispose entry since were not storing it
                entry.Dispose();
            }
        }

        /// <summary>
        /// NOOP
        /// </summary>
        public void CacheClear()
        { }

        ///<inheritdoc/>
        public void CacheHardClear()
        {
            //Take lock to remove the stored wait entires to dispose of them
            WaitEntry[] pooled;

            lock (StoreLock)
            {
                pooled = _entryPool.ToArray();
                _entryPool.Clear();

                //Cleanup the wait store
                _waitStore.TrimExcess(MaxPoolSize);
            }

            //Dispose entires
            Array.ForEach(pooled, static pooled => pooled.Dispose());
        }

        /// <summary>
        /// An entry within the lock table that 
        /// </summary>
        protected sealed class WaitEntry : IDisposable
        {
            private uint _waitCount;
            private readonly SemaphoreSlim _waitHandle;

            /// <summary>
            /// The session this entry is providing mutual exclusion to
            /// </summary>
            public TSession? Session { get; private set; }

            /// <summary>
            /// Initializes a new <see cref="WaitEntry"/>
            /// </summary>
            public WaitEntry()
            {
                _waitHandle = new(1, 1);
                Session = default!;
            }

            /// <summary>
            /// Gets a token used to enter the lock which may block, or yield async
            /// outside of a nested lock
            /// </summary>
            /// <returns>The waiter used to enter a wait on the moniker</returns>
            public WaitEnterToken GetWaiter()
            {
                /*
                 * Increment wait count before entering the lock
                 * A cancellation is the only way out, so cover that 
                 * during the async, only if the token is cancelable
                 */
                _ = Interlocked.Increment(ref _waitCount);
                return new(this);
            }

            /// <summary>
            /// Prepares a release 
            /// </summary>
            /// <param name="releaser">
            /// The token that should be used to release the exclusive lock held on 
            /// a moniker
            /// </param>
            /// <returns>The number of remaining waiters</returns>
            public uint Release(out WaitReleaseToken releaser)
            {
                releaser = new(_waitHandle);

                //Decrement release count before leaving
                return Interlocked.Decrement(ref _waitCount);
            }

            /// <summary>
            /// Prepres a new <see cref="WaitEntry"/> for 
            /// its new session.
            /// </summary>
            /// <param name="session">The session to hold a referrnce to</param>
            public void Prepare(TSession? session)
            {
                Session = session;
                _waitCount = 0;
            }

            /*
             * Called by WaitEnterToken to enter the lock 
             * outside a nested lock
             */

            internal Task WaitAsync(CancellationToken cancellation)
            {

                //See if lock can be entered synchronously
                if (_waitHandle.Wait(0, CancellationToken.None))
                {
                    //Lock was entered successfully without async yield
                    return Task.CompletedTask;
                }

                //Lock must be entered async

                //Check to confirm cancellation may happen
                if (cancellation.CanBeCanceled)
                {
                    //Task may be cancelled, so we need to monitor the results to properly set waiting count
                    Task wait = _waitHandle.WaitAsync(cancellation);
                    return WaitForLockEntryWithCancellationAsync(wait);
                }
                else
                {
                    //Task cannot be canceled, so we dont need to monitor the results
                    return _waitHandle.WaitAsync(CancellationToken.None);
                }
            }

            private async Task WaitForLockEntryWithCancellationAsync(Task wait)
            {
                try
                {
                    await wait.ConfigureAwait(false);
                }
                catch
                {
                    //Decrement wait count on error entering lock async
                    _ = Interlocked.Decrement(ref _waitCount);
                    throw;
                }
            }

            ///<inheritdoc/>
            public void Dispose()
            {
                _waitHandle.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// A token used to safely release an exclusive lock inside the 
        /// <see cref="WaitEntry"/>
        /// </summary>
        protected readonly ref struct WaitReleaseToken
        {
            private readonly SemaphoreSlim? _sem;

            internal WaitReleaseToken(SemaphoreSlim sem) => _sem = sem;

            /// <summary>
            /// Releases the exclusive lock held by the token. NOTE:
            /// this method may only be called ONCE after a wait has been
            /// released
            /// </summary>
            public readonly void Release() => _sem?.Release();
        }

        /// <summary>
        /// A token used to safely enter a wait for exclusive access to a <see cref="WaitEntry"/>
        /// </summary>
        protected readonly ref struct WaitEnterToken
        {
            private readonly WaitEntry _entry;

            internal WaitEnterToken(WaitEntry entry) => _entry = entry;
            
            /// <summary>
            /// Enters the wait for the WaitEntry. This method may not block
            /// or yield (IE Return <see cref="Task.CompletedTask"/>)
            /// </summary>
            /// <param name="cancellation">A token to cancel the wait for the resource</param>
            /// <returns></returns>
            public Task EnterWaitAsync(CancellationToken cancellation) => _entry.WaitAsync(cancellation);
        }
    }
}
