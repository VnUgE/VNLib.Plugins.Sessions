/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.Memory
* File: MemorySession.cs 
*
* MemorySession.cs is part of VNLib.Plugins.Essentials.Sessions.Memory which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.Memory is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.Memory is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Essentials.Sessions.Memory. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using VNLib.Plugins.Essentials.Extensions;

using VNLib.Net.Http;
using static VNLib.Plugins.Essentials.Sessions.ISessionExtensions;

#nullable enable

namespace VNLib.Plugins.Essentials.Sessions.Memory
{
    internal class MemorySession : SessionBase
    {
        private readonly Dictionary<string, string> DataStorage;

        private readonly Func<IHttpEvent, string, string> OnSessionUpdate;

        public MemorySession(string sessionId, IPAddress ipAddress, Func<IHttpEvent, string, string> onSessionUpdate)
        {
            //Set the initial is-new flag
            DataStorage = new Dictionary<string, string>(10);
            
            OnSessionUpdate = onSessionUpdate;
            //Get new session id
            SessionID = sessionId;
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
                //store new sessionid
                SessionID = OnSessionUpdate(state, SessionID);
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
                SessionID = OnSessionUpdate(state, SessionID);
            }
            //Clear flags
            Flags.ClearAll();
            //Memory session always completes 
            return ValueTask.FromResult<Task?>(null);
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
