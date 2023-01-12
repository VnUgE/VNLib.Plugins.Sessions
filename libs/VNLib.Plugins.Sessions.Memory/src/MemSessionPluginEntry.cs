/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Memory
* File: MemSessionPluginEntry.cs 
*
* MemSessionPluginEntry.cs is part of VNLib.Plugins.Sessions.Memory which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Sessions.Memory is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Sessions.Memory is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Sessions;

namespace VNLib.Plugins.Sessions.Memory
{
    /// <summary>
    /// Provides an IPlugin entrypoint for standalone memory sessions
    /// </summary>
    public sealed class MemSessionPluginEntry : PluginBase, ISessionProvider
    {
        ///<inheritdoc/>
        public override string PluginName => "Essentials.MemorySessions";

        private readonly MemorySessionEntrypoint ep = new();

        ///<inheritdoc/>
        protected override void OnLoad()
        {
            //Try to load 
            ep.Load(this, Log);
            Log.Information("Plugin loaded");
        }

        ///<inheritdoc/>
        protected override void OnUnLoad()
        {
            Log.Information("Plugin unloaded");
        }

        ///<inheritdoc/>
        protected override void ProcessHostCommand(string cmd)
        {
            throw new NotImplementedException();
        }

        ///<inheritdoc/>
        public ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return ep.GetSessionAsync(entity, cancellationToken);
        }
    }
}
