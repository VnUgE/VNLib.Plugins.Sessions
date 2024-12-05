/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: SessionProvider
* File: WebSessionSecMiddleware.cs 
*
* WebSessionSecMiddleware.cs is part of SessionProvider which is part of the larger 
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
using System.Threading.Tasks;
using System.Security.Authentication;
using System.Text.Json.Serialization;

using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Essentials.Middleware;

namespace VNLib.Plugins.Essentials.Sessions
{
    [ConfigurationName("web")]
    [MiddlewareImpl(MiddlewareImplOptions.SecurityCritical)]
    internal sealed class WebSessionSecMiddleware(PluginBase plugin, IConfigScope config) : IHttpMiddleware
    {
        private readonly ILogProvider _log = plugin.Log.CreateScope("Session-Sec");
        private readonly SecConfig _secConfig = config.Deserialze<SecConfig>();

        ///<inheritdoc/>
        public ValueTask<FileProcessArgs> ProcessAsync(HttpEntity entity)
        {
            ref readonly SessionInfo session = ref entity.Session;

            if (session.IsSet)
            {
                /*
                * Check if the session was established over a secure connection, 
                * and if the current connection is insecure, redirect them to a 
                * secure connection.
                */
                if (session.SecurityProcol > SslProtocols.None && !entity.IsSecure)
                {
                    //Redirect the client to https
                    UriBuilder ub = new(entity.Server.RequestUri)
                    {
                        Scheme = Uri.UriSchemeHttps
                    };

                    _log.Debug("Possbile session TLS downgrade detected, redirecting {con} to secure endpoint", entity.TrustedRemoteIp);

                    //Redirect
                    entity.Redirect(RedirectType.Moved, ub.Uri);
                    return ValueTask.FromResult(FileProcessArgs.VirtualSkip);
                }

                //If session is not new, then verify it matches stored credentials
                if (!session.IsNew && session.SessionType == SessionType.Web)
                {
                    if (_secConfig.EnfoceStrictTlsProtocol)
                    {
                        //Try to prevent security downgrade attacks
                        if (!(session.IPMatch && session.SecurityProcol <= entity.Server.GetSslProtocol()))
                        {
                            _log.Debug("Possible TLS downgrade attack stopeed from connection {con}", entity.TrustedRemoteIp);
                            return ValueTask.FromResult(FileProcessArgs.Deny);
                        }
                    }
                }
            }

            return ValueTask.FromResult(FileProcessArgs.Continue);
        }

        sealed class SecConfig
        {

            [JsonPropertyName("strict_tls_protocol")]
            public bool EnfoceStrictTlsProtocol { get; set; } = true;
        }
    }
}
