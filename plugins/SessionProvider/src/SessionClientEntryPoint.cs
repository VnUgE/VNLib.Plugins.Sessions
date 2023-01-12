/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: SessionProvider
* File: SessionClientEntryPoint.cs 
*
* SessionClientEntryPoint.cs is part of SessionProvider which is part of the larger 
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

using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Net.Http;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// The implementation type for dynamic loading of unified session providers 
    /// </summary>
    public sealed class SessionClientEntryPoint : PluginBase, ISessionProvider
    {       
        ///<inheritdoc/>
        public override string PluginName => "Essentials.Sessions";

        private RuntimeSessionProvider[] ProviderArray = Array.Empty<RuntimeSessionProvider>();
        

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
                    ProviderArray = providers.ToArray();

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
                providers.ForEach(static s => s.Dispose());

                Log.Warn("Plugin configuration was missing required variables {var}", knf.Message);
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
            //Cleanup assemblies
            ProviderArray.TryForeach(static p => p.Dispose());

            //Clear array
            ProviderArray = Array.Empty<RuntimeSessionProvider>();

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
