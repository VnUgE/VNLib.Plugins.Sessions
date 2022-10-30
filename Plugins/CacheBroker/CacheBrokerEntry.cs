using System;
using System.IO;
using System.Collections.Generic;

using VNLib.Utils.Logging;
using VNLib.Plugins.Cache.Broker.Endpoints;
using VNLib.Plugins.Extensions.Loading.Routing;

namespace VNLib.Plugins.Cache.Broker
{
    public sealed class CacheBrokerEntry : PluginBase
    {
        public override string PluginName => "Cache.Broker";

        protected override void OnLoad()
        {
            try
            {
                this.Route<BrokerRegistrationEndpoint>();

                Log.Information("Plugin loaded");
            }
            catch (FileNotFoundException)
            {
                Log.Error("Public key file was not found at the specified path");
            }
            catch (KeyNotFoundException knf)
            {
                Log.Error("Required configuration keys were not found {mess}", knf.Message);
            }
        }

        protected override void OnUnLoad()
        {
            Log.Debug("Plugin unloaded");
        }

        protected override void ProcessHostCommand(string cmd)
        {
            throw new NotImplementedException();
        }
    }
}
