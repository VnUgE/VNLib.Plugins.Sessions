/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: WebSession.cs 
*
* WebSession.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.VNCache is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.VNCache is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using VNLib.Net.Http;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Sessions.Cache.Client;
using static VNLib.Plugins.Essentials.Sessions.ISessionExtensions;

namespace VNLib.Plugins.Sessions.VNCache
{
    internal class WebSession : RemoteSession
    {
        protected readonly Func<IHttpEvent, string, string> UpdateId;
        private string? _oldId;

        public WebSession(string sessionId, IRemoteCacheStore client, TimeSpan backgroundTimeOut, Func<IHttpEvent, string, string> UpdateId)
            : base(sessionId, client, backgroundTimeOut)
        {
            this.UpdateId = UpdateId;
        }

        public override async Task WaitAndLoadAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            //Wait for the session to load
            await base.WaitAndLoadAsync(entity, cancellationToken);
            //If the session is new, set to web mode
            if (IsNew)
            {
                SessionType = SessionType.Web;
            }
        }

        private async Task ProcessUpgradeAsync()
        {
            //Setup timeout cancellation for the update, to cancel it
            using CancellationTokenSource cts = new(UpdateTimeout);
            await Client.AddOrUpdateObjectAsync(_oldId!, SessionID, DataStore, cts.Token);
            _oldId = null;
        }

        protected override ValueTask<Task?> UpdateResource(bool isAsync, IHttpEvent state)
        {
            Task? result = null;
            //Check flags in priority level, Invalid is highest state priority
            if (Flags.IsSet(INVALID_MSK))
            {
                //Clear all stored values
                DataStore!.Clear();
                //Reset ip-address
                UserIP = state.Server.GetTrustedIp();
                //Update created time
                Created = DateTimeOffset.UtcNow;
                //Init the new session-data
                this.InitNewSession(state.Server);
                //Restore session type
                SessionType = SessionType.Web;
                //generate new session-id and update the record in the store
                _oldId = SessionID;
                //Update the session-id
                SessionID = UpdateId(state, _oldId);
                //write update to server
                result = Task.Run(ProcessUpgradeAsync);
            }
            else if (Flags.IsSet(REGEN_ID_MSK))
            {
                //generate new session-id and update the record in the store
                _oldId = SessionID;
                //Update the session-id
                SessionID = UpdateId(state, _oldId);
                //Update created time
                Created = DateTimeOffset.UtcNow;
                //write update to server
                result = Task.Run(ProcessUpgradeAsync);
            }
            else if (Flags.IsSet(MODIFIED_MSK))
            {
                //Send update to server
                result = Task.Run(ProcessUpdateAsync);
            }
            
            //Clear all flags
            Flags.ClearAll();
            
            return ValueTask.FromResult<Task?>(null);
        }
    }
}
