/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.VNCache
* File: WebSessionIdFactory.cs 
*
* WebSessionIdFactory.cs is part of VNLib.Plugins.Essentials.Sessions.VNCache which is part of the larger 
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

using VNLib.Hashing;
using VNLib.Net.Http;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Sessions.Cache.Client;

namespace VNLib.Plugins.Sessions.VNCache
{
    /// <summary>
    /// <see cref="ISessionIdFactory"/> implementation, using 
    /// http cookies as session id storage
    /// </summary>
    [ConfigurationName(WebSessionProvider.WEB_SESSION_CONFIG)]
    internal sealed class WebSessionIdFactory : ISessionIdFactory
    {
        ///<inheritdoc/>
        public bool RegenerationSupported { get; } = true;

        ///<inheritdoc/>
        public bool RegenIdOnEmptyEntry { get; } = true;

        private readonly int _cookieSize;
        private readonly SingleCookieController _cookieController;

        /// <summary>
        /// Initialzies a new web session Id factory
        /// </summary>
        /// <param name="cookieSize">The size of the cookie in bytes</param>
        /// <param name="sessionCookieName">The name of the session cookie</param>
        /// <param name="validFor">The time the session cookie is valid for</param>
        public WebSessionIdFactory(uint cookieSize, string sessionCookieName, TimeSpan validFor)
        {
            _cookieSize = (int)cookieSize;

            //Create cookie controller
            _cookieController = new(sessionCookieName, validFor)
            {
                Domain = null,
                Path = "/",
                SameSite = CookieSameSite.Lax,
                Secure = true,
                HttpOnly = true
            };
        }

        //Create instance from config
        public WebSessionIdFactory(PluginBase plugin, IConfigScope config):
            this(
                config.GetRequiredProperty("cookie_size", p => p.GetUInt32()),
                config.GetRequiredProperty("cookie_name", p => p.GetString()!),
                config.GetRequiredProperty("valid_for_sec", p => p.GetTimeSpan(TimeParseType.Seconds))
            )
        { }


        public string RegenerateId(IHttpEvent entity)
        {
            //Random hex hash
            string sessionId = RandomHash.GetRandomBase32(_cookieSize);

            //Set cookie
            _cookieController.SetCookie(entity, sessionId);

            //return session-id value from cookie value
            return sessionId;
        }

        public string? TryGetSessionId(IHttpEvent entity)
        {
            //Handle empty cookie values
            string? existingId = _cookieController.GetCookie(entity);

            return string.IsNullOrWhiteSpace(existingId) ? null : existingId;
        }

        public bool CanService(IHttpEvent entity)
        {
            return entity.Server.RequestCookies.ContainsKey(_cookieController.Name) || entity.Server.UserAgent != null;
        }
    }
}