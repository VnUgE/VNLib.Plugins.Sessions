/*
* Copyright (c) 2024 Vaughn Nugent
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Logging;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Routing;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// The implementation type for dynamic loading of unified session providers 
    /// </summary>
    public sealed class SessionProviderEntry : PluginBase
    {
        ///<inheritdoc/>
        public override string PluginName => "Essentials.Sessions";

        ///<inheritdoc/>
        protected override void OnLoad()
        {
            List<RuntimeSessionProvider> providers = [];

            Log.Verbose("Loading all specified session providers");

            //Get all provider names
            string[] providerAssemblyNames = this.GetConfig("provider_assemblies")
                .Deserialze<string[]>()
                .Where(s => s != null)
                .ToArray();

            foreach (string asm in providerAssemblyNames)
            {
                Log.Verbose("Loading {dll} session provider", asm);

                try
                {
                    //Attempt to load provider
                    ISessionProvider prov = this.CreateServiceExternal<ISessionProvider>(asm);

                    providers.Add(new(prov));
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to load session provider {dll}:\n{error}", asm, ex);
                }
            }

            if (providers.Count > 0)
            {

                //Service container will dispose when the plugin lifecycle has complted
#pragma warning disable CA2000 // Dispose objects before losing scope
                SessionProvider provider = new([.. providers]);
#pragma warning restore CA2000 // Dispose objects before losing scope

                Log.Information("Loaded {count} session providers", providers.Count);

                //Export the session provider as it's interface type 
                this.ExportService<ISessionProvider>(provider);
            }
            else
            {
                Log.Information("No session providers loaded");
            }

            //See if web sessions are loaded
            if (this.HasConfigForType<WebSessionSecMiddleware>())
            {
                //Init web session sec middlware
                this.Middleware()
                    .Add(this.GetOrCreateSingleton<WebSessionSecMiddleware>());

                Log.Debug("Web session security middleware initialized");
            }

            Log.Information("Plugin loaded");
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

        private sealed class SessionProvider(RuntimeSessionProvider[] loaded) : VnDisposeable, ISessionProvider
        {
            //Default to an empty array for default support even if no runtime providers are loaded
            private RuntimeSessionProvider[] ProviderArray = loaded;

            ValueTask<SessionHandle> ISessionProvider.GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
            {
                RuntimeSessionProvider p;

                //Loop through providers
                for (int i = 0; i < ProviderArray.Length; i++)
                {
                    p = ProviderArray[i];

                    //Check if provider can process the entity
                    if (p.CanProcess(entity))
                    {
                        //Get session
                        return p.GetSessionAsync(entity, cancellationToken);
                    }
                }

                //Return empty session
                return ValueTask.FromResult(SessionHandle.Empty);
            }

            protected override void Free()
            {
                //Remove current providers so we can dispose them 
                ProviderArray = [];
            }
        }
    }
}
