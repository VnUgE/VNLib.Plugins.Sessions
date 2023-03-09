/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: SessionProvider
* File: SessionProviderEntry.cs 
*
* SessionProviderEntry.cs is part of SessionProvider which is part of the larger 
* VNLib collection of libraries and utilities.
*
* SessionProvider is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* SessionProvider is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.Design;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Attributes;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// The implementation type for dynamic loading of unified session providers 
    /// </summary>
    public sealed class SessionProviderEntry : PluginBase
    {       
        ///<inheritdoc/>
        public override string PluginName => "Essentials.Sessions";

        private SessionProvider? _provider;

        /*
         * Declare a service configuration method to 
         * expose the session provider
         */

        [ServiceConfigurator]
        public void ConfigureServices(IServiceContainer services)
        {
            //publish the service
            services.AddService(typeof(ISessionProvider), _provider);
        }
       

        protected override void OnLoad()
        {
            List<RuntimeSessionProvider> providers = new();

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
                    AssemblyLoader<ISessionProvider> prov =  this.LoadAssembly<ISessionProvider>(asm);

                    try
                    {
                        //Create localized log
                        LocalizedLogProvider log = new(Log, $"{Path.GetFileName(asm)}");

                        RuntimeSessionProvider p = new(prov);

                        //Call load method
                        p.Load(this, log);

                        //Add to list
                        providers.Add(p);
                    }
                    catch
                    {
                        prov.Dispose();
                        throw;
                    }
                }

                if(providers.Count > 0)
                {
                    //Create array for searching for providers
                    _provider = new(providers.ToArray());

                    Log.Information("Loaded {count} session providers", providers.Count);
                }
                else
                {
                    Log.Information("No session providers loaded");
                }

                Log.Information("Plugin loaded");
            }
            catch
            {
                //Dispose providers
                providers.ForEach(static s => s.Dispose());
                throw;
            }
        }
      
        protected override void OnUnLoad()
        {
            Log.Information("Plugin unloaded");
        }

        protected override void ProcessHostCommand(string cmd)
        {
            if (!this.IsDebug())
            {
                return;
            }
        }

        /*
         * When exposing the session provider as a service, it may be disposed by the 
         * service container if its delcared as disposable. 
         */

        private sealed class SessionProvider : VnDisposeable, ISessionProvider, IDisposable
        {
            private RuntimeSessionProvider[] ProviderArray = Array.Empty<RuntimeSessionProvider>();

            public SessionProvider(RuntimeSessionProvider[] loaded)
            {
                ProviderArray = loaded;
            }

            ValueTask<SessionHandle> ISessionProvider.GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
            {
                //Loop through providers
                for (int i = 0; i < ProviderArray.Length; i++)
                {
                    //Check if provider can process the entity
                    if (ProviderArray[i].CanProcess(entity))
                    {
                        //Get session
                        return ProviderArray[i].GetSessionAsync(entity, cancellationToken);
                    }
                }

                //Return empty session
                return new ValueTask<SessionHandle>(SessionHandle.Empty);
            }

            protected override void Free()
            {
                //Remove current providers so we can dispose them 
                RuntimeSessionProvider[] current = Interlocked.Exchange(ref ProviderArray, Array.Empty<RuntimeSessionProvider>());

                //Cleanup assemblies
                current.TryForeach(static p => p.Dispose());
            }
        }
    }
}
