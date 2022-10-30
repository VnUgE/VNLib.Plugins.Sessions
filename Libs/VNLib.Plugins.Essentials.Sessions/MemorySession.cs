using System;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Extensions;

using static VNLib.Plugins.Essentials.Sessions.ISessionExtensions;

#nullable enable

namespace VNLib.Plugins.Essentials.Sessions.Memory
{
    internal class MemorySession : SessionBase
    {
        private readonly Dictionary<string, string> DataStorage;

        private readonly MemorySessionStore SessionStore;

        public MemorySession(IPAddress ipAddress, MemorySessionStore SessionStore)
        {
            //Set the initial is-new flag
            DataStorage = new Dictionary<string, string>(10);
            this.SessionStore = SessionStore;
            //Get new session id
            SessionID = SessionStore.NewSessionID;
            UserIP = ipAddress;
            SessionType = SessionType.Web;
            Created = DateTimeOffset.UtcNow;
            //Init 
            IsNew = true;
        }
        //Store in memory directly
        public override IPAddress UserIP { get; protected set; }

        //Session type has no backing store, so safe to hard-code it's always web

        public override SessionType SessionType => SessionType.Web;

        protected override ValueTask<Task?> UpdateResource(bool isAsync, IHttpEvent state)
        {
            //if invalid is set, invalide the current session
            if (Flags.IsSet(INVALID_MSK))
            {
                //Clear storage, and regenerate the sessionid
                DataStorage.Clear();
                RegenId(state);
                //Reset ip-address
                UserIP = state.Server.GetTrustedIp();
                //Update created-time
                Created = DateTimeOffset.UtcNow;
                //Re-initialize the session to the state of the current connection
                this.InitNewSession(state.Server);
                //Modified flag doesnt matter since there is no write-back
               
            }
            else if (Flags.IsSet(REGEN_ID_MSK))
            {
                //Regen id without modifying the data store
                RegenId(state);
            }
            //Clear flags
            Flags.ClearAll();
            //Memory session always completes 
            return ValueTask.FromResult<Task?>(null);
        }

        private void RegenId(IHttpEvent entity)
        {
            //Get a new session-id
            string newId = SessionStore.NewSessionID;
            //Update the cache entry
            SessionStore.UpdateRecord(newId, this);
            //store new sessionid
            SessionID = newId;
            //set cookie
            SessionStore.SetSessionCookie(entity, this);
        }       

        protected override Task OnEvictedAsync()
        {
            //Clear all session data
            DataStorage.Clear();
            return Task.CompletedTask;
        }

        protected override string IndexerGet(string key)
        {
            return DataStorage.GetValueOrDefault(key, string.Empty);
        }

        protected override void IndexerSet(string key, string value)
        {
            //Check for special keys
            switch (key)
            {
                //For tokens/login hashes, we can set the upgrade flag
                case TOKEN_ENTRY:
                case LOGIN_TOKEN_ENTRY:
                    Flags.Set(REGEN_ID_MSK);
                    break;
            }
            DataStorage[key] = value;
        }
    }
}
