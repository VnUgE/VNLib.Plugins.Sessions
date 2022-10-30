using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.VisualStudio.Threading;

using VNLib.Net.Http;
using VNLib.Data.Caching;
using VNLib.Data.Caching.Exceptions;
using VNLib.Utils.Extensions;
using VNLib.Net.Messaging.FBM.Client;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;

#nullable enable

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// Base class for cacheable lazy initialized session entires 
    /// that exist in a remote caching server
    /// </summary>
    public abstract class RemoteSession : SessionBase
    {
        protected const string CREATED_TIME_ENTRY = "__.i.ctime";

        protected readonly FBMClient Client;
        protected readonly TimeSpan UpdateTimeout;

        private readonly AsyncLazyInitializer Initializer;
        
        /// <summary>
        /// The lazy loaded data-store
        /// </summary>
        protected Dictionary<string, string>? DataStore;

        public RemoteSession(string sessionId, FBMClient client, TimeSpan backgroundTimeOut)
        {
            SessionID = sessionId;
            UpdateTimeout = backgroundTimeOut;
            Client = client;
            Initializer = new(InitializeAsync, null);
        }

        /// <summary>
        /// The data initializer, loads the data store from the connected cache server
        /// </summary>
        /// <returns>A task that completes when the get operation completes</returns>
        protected virtual async Task InitializeAsync()
        {
            //Setup timeout cancellation for the get, to cancel it
            using CancellationTokenSource cts = new(UpdateTimeout);
            //get or create a new session
            DataStore = await Client.GetObjectAsync<Dictionary<string, string>>(SessionID, cancellationToken: cts.Token);
        }
        /// <summary>
        /// Updates the current sessin agaisnt the cache store
        /// </summary>
        /// <returns>A task that complets when the update has completed</returns>
        protected virtual async Task ProcessUpdateAsync()
        {
            //Setup timeout cancellation for the update, to cancel it
            using CancellationTokenSource cts = new(UpdateTimeout);
            await Client.AddOrUpdateObjectAsync(SessionID, null, DataStore, cts.Token);
        }
        /// <summary>
        /// Delets the current session in the remote store
        /// </summary>
        /// <returns>A task that completes when instance has been deleted</returns>
        protected virtual async Task ProcessDeleteAsync()
        {
            //Setup timeout cancellation for the update, to cancel it
            using CancellationTokenSource cts = new(UpdateTimeout);
            try
            {
                await Client.DeleteObjectAsync(SessionID, cts.Token);
            }
            catch (ObjectNotFoundException)
            {
                //This is fine, if the object does not exist, nothing to invalidate
            }
        }
        
        ///<inheritdoc/>
        public override DateTimeOffset Created
        {
            get
            {
                //Deserialze the base32 ms
                long unixMs = this.GetValueType<string, long>(CREATED_TIME_ENTRY);
                //set created time from ms
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
            }

            protected set => this.SetValueType(CREATED_TIME_ENTRY, value.ToUnixTimeMilliseconds());
        }
        ///<inheritdoc/>
        protected override string IndexerGet(string key)
        {
            //Get the value at the key or an empty string as a default
            return DataStore!.GetValueOrDefault(key, string.Empty);
        }
        ///<inheritdoc/>
        protected override void IndexerSet(string key, string value)
        {
            //If the value is null, remove the key from the store
            if (value == null)
            {
                //Set modified flag 
                IsModified |= DataStore!.Remove(key);
            }
            else
            {
                //Store the value at the specified key
                DataStore![key] = value;
                IsModified = true;
            }
        }

        
        /*
         * If the data-store is not found it means the session does not 
         * exist in cache, so its technically not dangerous to reuse,
         * so the new mask needs to be set, but the old ID is going 
         * to be reused
         */

        /// <summary>
        /// Waits for exclusive access to the session, and initializes
        /// session data (loads it from the remote store)
        /// </summary>
        /// <param name="entity">The event to attach a session to</param>
        /// <param name="cancellationToken">A token to cancel the operaion</param>
        /// <returns></returns>
        public virtual async Task WaitAndLoadAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            //Wait for exclusive access
            await base.WaitOneAsync(cancellationToken);
            try
            {
                //Lazily initalize the current instance 
                await Initializer.InitializeAsync(cancellationToken);
                //See if data-store is null (new session was created
                if (DataStore == null)
                {
                    //New session was created
                    DataStore = new(10);
                    //Set is-new flag
                    Flags.Set(IS_NEW_MSK);
                    //Set created time
                    Created = DateTimeOffset.UtcNow;
                    //Init ipaddress
                    UserIP = entity.Server.GetTrustedIp();
                    //Set modified flag so session will be updated
                    IsModified = true;
                }
            }
            catch
            {
                MainLock.Release();
                throw;
            }
        }
        ///<inheritdoc/>
        protected override Task OnEvictedAsync()
        {
            //empty the dict to help the GC
            DataStore!.Clear();
            return Task.CompletedTask;
        }
    }
}
