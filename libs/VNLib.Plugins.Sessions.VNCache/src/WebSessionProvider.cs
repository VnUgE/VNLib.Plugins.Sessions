/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: WebSessionProvider.cs 
*
* WebSessionProvider.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.VNCache is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.VNCache is distributed in the hope that it will be useful,
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
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Sessions.VNCache
{

    [ConfigurationName(WebSessionProviderEntry.WEB_SESSION_CONFIG)]
    internal sealed class WebSessionProvider : ISessionProvider
    {
        private static readonly SessionHandle _vf =  new (null, FileProcessArgs.VirtualSkip, null);

        private readonly TimeSpan _validFor;
        private readonly WebSessionStore _sessions;
        private readonly uint _maxConnections;

        private uint _waitingConnections;

        public bool IsConnected => _sessions.IsConnected;

        public WebSessionProvider(PluginBase plugin, IConfigScope config)
        {
            _validFor = config["valid_for_sec"].GetTimeSpan(TimeParseType.Seconds);
            _maxConnections = config["max_waiting_connections"].GetUInt32();

            //Init session provider
            _sessions = plugin.GetOrCreateSingleton<WebSessionStore>();
        }

        private SessionHandle PostProcess(WebSession? session)
        {
            if (session == null)
            {
                return SessionHandle.Empty;
            }

            //Make sure the session has not expired yet
            if (session.Created.Add(_validFor) < DateTimeOffset.UtcNow)
            {
                //Invalidate the session, so its technically valid for this request, but will be cleared on this handle close cycle
                session.Invalidate();

                //Clear basic login status
                session.Token = null;
                session.UserID = null;
                session.Privilages = 0;
                session.SetLoginToken(null);
            }

            return new SessionHandle(session, OnSessionReleases);
        }

        private ValueTask OnSessionReleases(ISession session, IHttpEvent entity) => _sessions.ReleaseSessionAsync((WebSession)session, entity);

        public ValueTask<SessionHandle> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            //Limit max number of waiting clients and make sure were connected
            if (!_sessions.IsConnected || _waitingConnections > _maxConnections)
            {
                //Set 503 for temporary unavail
                entity.CloseResponse(System.Net.HttpStatusCode.ServiceUnavailable);
                return ValueTask.FromResult(_vf);
            }

            ValueTask<WebSession?> result = _sessions.GetSessionAsync(entity, cancellationToken);

            if (result.IsCompleted)
            {
                WebSession? session = result.GetAwaiter().GetResult();

                //Post process and get handle for session
                SessionHandle handle = PostProcess(session);

                return ValueTask.FromResult(handle);
            }
            else
            {
                return new(AwaitAsyncGet(result));
            }
        }

        private async Task<SessionHandle> AwaitAsyncGet(ValueTask<WebSession?> async)
        {
            //Inct wait count while async waiting
            Interlocked.Increment(ref _waitingConnections);
            try
            {
                //await the session
                WebSession? session = await async.ConfigureAwait(false);

                //return empty session handle if the session could not be found
                return PostProcess(session);
            }
            finally
            {
                Interlocked.Decrement(ref _waitingConnections);
            }
        }
    }
}
