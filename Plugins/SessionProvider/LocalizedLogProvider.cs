using System;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Essentials.Sessions
{
    internal sealed class LocalizedLogProvider : ILogProvider
    {
        private readonly ILogProvider Log;
        private readonly string LogPrefix;

        public LocalizedLogProvider(ILogProvider log, string prefix)
        {
            Log = log;
            LogPrefix = prefix;
        }

        public void Flush()
        {
            Log.Flush();
        }

        public object GetLogProvider()
        {
            return Log.GetLogProvider();
        }

        public void Write(LogLevel level, string value)
        {
            Log.Write(level, $"[{LogPrefix}]: {value}");
        }

        public void Write(LogLevel level, Exception exception, string value = "")
        {
            Log.Write(level, exception, $"[{LogPrefix}]: {value}");
        }

        public void Write(LogLevel level, string value, params object[] args)
        {
            Log.Write(level, $"[{LogPrefix}]: {value}", args);
        }

        public void Write(LogLevel level, string value, params ValueType[] args)
        {
            Log.Write(level, $"[{LogPrefix}]: {value}", args);
        }
    }
}
