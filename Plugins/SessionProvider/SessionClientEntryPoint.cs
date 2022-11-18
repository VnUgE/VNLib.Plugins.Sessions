using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Essentials.Sessions.Runtime;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// The implementation type for dynamic loading of unified session providers 
    /// </summary>
    public sealed class SessionClientEntryPoint : PluginBase, ISessionProvider
    {        
        public override string PluginName => "Essentials.Sessions";

        
        private readonly List<AssemblyLoader<IRuntimeSessionProvider>> ProviderLoaders = new();

        private IRuntimeSessionProvider[] ProviderArray = Array.Empty<IRuntimeSessionProvider>();
        

        ValueTask<SessionHandle> ISessionProvider.GetSessionAsync(IHttpEvent entity, CancellationToken token)
        {
            //Loop through providers
            for (int i = 0; i < ProviderArray.Length; i++)
            {
                //Check if provider can process the entity
                if (ProviderArray[i].CanProcess(entity))
                {
                    //Get session
                    return ProviderArray[i].GetSessionAsync(entity, token);
                }
            }

            //Return empty session
            return new ValueTask<SessionHandle>(SessionHandle.Empty);
        }

        protected override void OnLoad()
        {
            try
            {
                Log.Verbose("Loading all specified session providers");

                //Get all provider names
                IEnumerable<string> providerAssemblyNames = PluginConfig.GetProperty("provider_assemblies")
                                                .EnumerateArray()
                                                .Where(s => s.GetString() != null)
                                                .Select(s => s.GetString()!);
                
               
                foreach(string asm in providerAssemblyNames)
                {
                    Log.Verbose("Loading {dll} session provider", asm);
    
                    //Attempt to load provider
                    AssemblyLoader<IRuntimeSessionProvider> prov =  this.LoadAssembly<IRuntimeSessionProvider>(asm);

                    //Create localized log
                    LocalizedLogProvider log = new(Log, $"{Path.GetFileName(asm)}");

                    //Try to load the websessions
                    prov.Resource.Load(this, log);

                    //Add provider to list
                    ProviderLoaders.Add(prov);
                }

                if(ProviderLoaders.Count > 0)
                {
                    //Create array for searching for providers
                    ProviderArray = ProviderLoaders.Select(s => s.Resource).ToArray();

                    Log.Information("Loaded {count} session providers", ProviderArray.Length);
                }
                else
                {
                    Log.Information("No session providers loaded");
                }

                Log.Information("Plugin loaded");
            }
            catch (KeyNotFoundException knf)
            {
                //Dispose providers
                ProviderLoaders.ForEach(s => s.Dispose());

                Log.Warn("Plugin configuration was missing required variables {var}", knf.Message);
            }
            catch
            {
                //Dispose providers
                ProviderLoaders.ForEach(s => s.Dispose());
                throw;
            }
        }
      
        protected override void OnUnLoad()
        {
            //Clear array
            ProviderArray = Array.Empty<IRuntimeSessionProvider>();

            //Cleanup assemblies
            ProviderLoaders.ForEach(p => p.Dispose());
            ProviderLoaders.Clear();

            Log.Information("Plugin unloaded");
        }

        protected override void ProcessHostCommand(string cmd)
        {
            if (!this.IsDebug())
            {
                return;
            }
        }
    }
}
