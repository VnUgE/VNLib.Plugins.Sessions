/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.Runtime
* File: IRuntimeSessionProvider.cs 
*
* IRuntimeSessionProvider.cs is part of VNLib.Plugins.Essentials.Sessions.Runtime which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.Runtime is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.Runtime is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using VNLib.Net.Http;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.Sessions.Runtime
{
    /// <summary>
    /// Represents a dynamically loadable type that an provide sessions to http connections
    /// </summary>
    public interface IRuntimeSessionProvider : ISessionProvider
    {
        /// <summary>
        /// Called immediatly after the plugin is loaded into the appdomain
        /// </summary>
        /// <param name="plugin">The plugin instance that is loading the module</param>
        /// <param name="localizedLog">The localized log provider for the provider</param>
        void Load(PluginBase plugin, ILogProvider localizedLog);

        /// <summary>
        /// Determines if the provider can return a session for the connection
        /// </summary>
        /// <param name="entity">The entity to process</param>
        /// <returns>A value indicating if this provider should be called to load a session for</returns>
        bool CanProcess(IHttpEvent entity);
    }
}
