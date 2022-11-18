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
