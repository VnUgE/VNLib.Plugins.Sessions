using VNLib.Net.Http;
using VNLib.Data.Caching;
using VNLib.Net.Messaging.FBM.Client;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Sessions.Cache.Client;
using static VNLib.Plugins.Essentials.Sessions.ISessionExtensions;


namespace VNLib.Plugins.Essentials.Sessions.VNCache
{
    internal class WebSession : RemoteSession
    {
        protected const ulong UPGRADE_MSK =     0b0000000000010000UL;

        protected readonly Func<IHttpEvent, string, string> UpdateId;
        private string? _oldId;

        public WebSession(string sessionId, FBMClient client, TimeSpan backgroundTimeOut, Func<IHttpEvent, string, string> UpdateId)
            : base(sessionId, client, backgroundTimeOut)
        {
            this.UpdateId = UpdateId;
        }

        protected override void IndexerSet(string key, string value)
        {
            //Set value
            base.IndexerSet(key, value);
            switch (key)
            {
                //Set the upgrade flag when token data is modified
                case LOGIN_TOKEN_ENTRY:
                case TOKEN_ENTRY:
                    Flags.Set(UPGRADE_MSK);
                    break;
            }
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
            else if (Flags.IsSet(UPGRADE_MSK | REGEN_ID_MSK))
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
