/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: RemoteSession.cs 
*
* RemoteSession.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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
using System.Collections.Generic;

using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;


namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// Base class for cacheable lazy initialized session entires 
    /// that exist in a remote caching server
    /// </summary>
    public abstract class RemoteSession : SessionBase, IRemoteSession
    {
        protected const string CREATED_TIME_ENTRY = "__.i.ctime";

        /// <summary>
        /// The session data store
        /// </summary>
        protected readonly IDictionary<string, string> DataStore;

        /// <summary>
        /// The reason that the session was destroyed if an error occured
        /// </summary>
        protected Exception? ErrorCause { get; set; }

        /// <summary>
        /// Initialzies a new session
        /// </summary>
        /// <param name="sessionId">The id of the current session</param>
        /// <param name="initialData">The initial data</param>
        /// <param name="isNew">A flag that determines if the session is considered new</param>
        protected RemoteSession(string sessionId, IDictionary<string, string> initialData, bool isNew)
        {
            SessionID = sessionId;
            DataStore = initialData;
            IsNew = isNew;
        }

        ///<inheritdoc/>
        public override string SessionID { get; }

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

            set => this.SetValueType(CREATED_TIME_ENTRY, value.ToUnixTimeMilliseconds());
        }     

        ///<inheritdoc/>
        protected override string IndexerGet(string key)
        {
            //Get the value at the key or an empty string as a default
            return DataStore.GetValueOrDefault(key) ?? string.Empty;
        }
        ///<inheritdoc/>
        protected override void IndexerSet(string key, string value)
        {
            //If the value is null, remove the key from the store
            if (value == null)
            {
                //Set modified flag 
                IsModified |= DataStore.Remove(key);
            }
            else
            {
                //Store the value at the specified key
                DataStore[key] = value;
                IsModified = true;
            }
        }
    

        ///<inheritdoc/>
        public virtual SessionStatus GetStatus()
        {
            SessionStatus status = SessionStatus.None;

            status |= Flags.IsSet(INVALID_MSK) ? SessionStatus.Delete : SessionStatus.None;
            status |= Flags.IsSet(REGEN_ID_MSK) ? SessionStatus.RegenId : SessionStatus.None;
            status |= Flags.IsSet(MODIFIED_MSK) ? SessionStatus.UpdateOnly: SessionStatus.None;
            status |= Flags.IsSet(DETACHED_MSK) ? SessionStatus.Detach: SessionStatus.None;
            
            return status;
        }

        ///<inheritdoc/>
        public virtual IDictionary<string, string> GetSessionData() => DataStore;

        ///<inheritdoc/>
        public virtual void Destroy(Exception? cause)
        {
            //Set invalid status
            ErrorCause = cause;
            Flags.Set(INVALID_MSK);
            
            //Clear all session data
            DataStore.Clear();
        }

        ///<inheritdoc/>
        public virtual bool IsValid(out Exception? cause)
        {
            /*
             * Were reusing the invalid mask assuming that when a session is invalidated
             * it will be deleted and destroyed
             */

            cause = ErrorCause;
            return !Flags.IsSet(INVALID_MSK);
        }

        ///<inheritdoc/>
        public virtual void SessionUpdateComplete()
        {
            //Reset flags
            Flags.ClearAll();
        }
    }
}
