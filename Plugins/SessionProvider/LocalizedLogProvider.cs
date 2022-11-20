/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: SessionProvider
* File: LocalizedLogProvider.cs 
*
* LocalizedLogProvider.cs is part of SessionProvider which is part of the larger 
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
