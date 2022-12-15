/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.Memory
* File: MemorySessionEntrypoint.cs 
*
* MemorySessionEntrypoint.cs is part of VNLib.Plugins.Essentials.Sessions.Memory which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.Memory is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.Memory is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Extensions.Loading.Events;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Essentials.Sessions.Runtime;

#nullable enable

namespace VNLib.Plugins.Essentials.Sessions.Memory
{
    public sealed class MemorySessionEntrypoint : IRuntimeSessionProvider, IIntervalScheduleable
    {
        const string WEB_SESSION_CONFIG = "web";

        private MemorySessionStore? _sessions;

        bool IRuntimeSessionProvider.CanProcess(IHttpEvent entity)
        {
            //Web sessions can always be provided
            return _sessions != null;
        }

        public ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            return _sessions!.GetSessionAsync(entity, cancellationToken);
        }

        void IRuntimeSessionProvider.Load(PluginBase plugin, ILogProvider localized)
        {
            //Get websessions config element

            IReadOnlyDictionary<string, JsonElement> webSessionConfig = plugin.GetConfig(WEB_SESSION_CONFIG);

            MemorySessionConfig config = new()
            {
                SessionLog = localized,
                MaxAllowedSessions = webSessionConfig["cache_size"].GetInt32(),
                SessionIdSizeBytes = webSessionConfig["cookie_size"].GetUInt32(),
                SessionTimeout = webSessionConfig["valid_for_sec"].GetTimeSpan(TimeParseType.Seconds),
                SessionCookieID = webSessionConfig["cookie_name"].GetString() ?? throw new KeyNotFoundException($"Missing required element 'cookie_name' for config '{WEB_SESSION_CONFIG}'"),
            };

            _sessions = new(config);

            //Begin listening for expired records
            _ = plugin.DeferTask(() => _sessions.CleanupExiredAsync(localized, plugin.UnloadToken));
         
            //Schedule garbage collector
            _ = plugin.ScheduleInterval(this, TimeSpan.FromMinutes(1));

            //Call cleanup on exit
            _ = plugin.UnloadToken.RegisterUnobserved(_sessions.Cleanup);
        }

        Task IIntervalScheduleable.OnIntervalAsync(ILogProvider log, CancellationToken cancellationToken)
        {
            //Cleanup expired sessions on interval
            _sessions?.GC();
            return Task.CompletedTask;
        }
    }
}
