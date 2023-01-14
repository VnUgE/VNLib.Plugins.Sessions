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
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using VNLib.Plugins.Essentials.Extensions;

using VNLib.Utils.Async;
using VNLib.Net.Http;
using VNLib.Utils.Memory.Caching;
using VNLib.Plugins.Essentials.Sessions;
using static VNLib.Plugins.Essentials.Sessions.ISessionExtensions;

namespace VNLib.Plugins.Sessions.Memory
{
    internal class MemorySession : SessionBase, ICacheable
    {
        private readonly Dictionary<string, string> DataStorage;

        private readonly Func<IHttpEvent, string, string> OnSessionUpdate;
        private readonly AsyncQueue<MemorySession> ExpiredTable;

        public MemorySession(string sessionId, IPAddress ipAddress, Func<IHttpEvent, string, string> onSessionUpdate, AsyncQueue<MemorySession> expired)
        {
            //Set the initial is-new flag
            DataStorage = new Dictionary<string, string>(10);
            ExpiredTable = expired;

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

        protected override string IndexerGet(string key)
        {
            return DataStorage.GetValueOrDefault(key, string.Empty);
        }

        protected override void IndexerSet(string key, string value)
        {
            //Check for special keys
            switch (key)
            {
                //For token/login hashes, we can set the upgrade flag
                case LOGIN_TOKEN_ENTRY:
                case TOKEN_ENTRY:
                    Flags.Set(REGEN_ID_MSK);
                    break;
            }
            DataStorage[key] = value;
        }


        DateTime ICacheable.Expires { get; set; }

        void ICacheable.Evicted()
        {
            DataStorage.Clear();
            //Enque cleanup
            _ = ExpiredTable.TryEnque(this);
        }

        bool IEquatable<ICacheable>.Equals(ICacheable? other) => other is ISession ses && SessionID.Equals(ses.SessionID, StringComparison.Ordinal);

    }
}
