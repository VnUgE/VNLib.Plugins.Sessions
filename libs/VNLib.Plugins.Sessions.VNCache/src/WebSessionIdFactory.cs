/*
* Copyright (c) 2023 Vaughn Nugent
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
using System.Collections.Generic;

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
    [ConfigurationName(WebSessionProviderEntry.WEB_SESSION_CONFIG)]
    internal sealed class WebSessionIdFactory : ISessionIdFactory
    {
        public TimeSpan ValidFor { get; }

        ///<inheritdoc/>
        public bool RegenerationSupported { get; } = true;
        ///<inheritdoc/>
        public bool RegenIdOnEmptyEntry { get; } = true;
       

        private readonly string SessionCookieName;
        private readonly int _cookieSize;

        /// <summary>
        /// Initialzies a new web session Id factory
        /// </summary>
        /// <param name="cookieSize">The size of the cookie in bytes</param>
        /// <param name="sessionCookieName">The name of the session cookie</param>
        /// <param name="validFor">The time the session cookie is valid for</param>
        public WebSessionIdFactory(uint cookieSize, string sessionCookieName, TimeSpan validFor)
        {
            ValidFor = validFor;
            SessionCookieName = sessionCookieName;
            _cookieSize = (int)cookieSize;
        }

        public WebSessionIdFactory(PluginBase pbase, IConfigScope config)
        {
            _cookieSize = (int)config["cookie_size"].GetUInt32();
            SessionCookieName = config["cookie_name"].GetString() 
                ?? throw new KeyNotFoundException($"Missing required element 'cookie_name' for config '{WebSessionProviderEntry.WEB_SESSION_CONFIG}'");           
            ValidFor = config["valid_for_sec"].GetTimeSpan(TimeParseType.Seconds);
        }


        public string RegenerateId(IHttpEvent entity)
        {
            //Random hex hash
            string sessionId = RandomHash.GetRandomBase32(_cookieSize);

            //Create new cookie
            HttpCookie cookie = new(SessionCookieName, sessionId)
            {
                ValidFor = ValidFor,
                Secure = true,
                HttpOnly = true,
                Domain = null,
                Path = "/",
                SameSite = CookieSameSite.Lax
            };

            //Set the session id cookie
            entity.Server.SetCookie(cookie);

            //return session-id value from cookie value
            return sessionId;
        }

        public string? TryGetSessionId(IHttpEvent entity)
        {
            //Get session cookie
            return entity.Server.RequestCookies.GetValueOrDefault(SessionCookieName);
        }

        public bool CanService(IHttpEvent entity)
        {
            return entity.Server.RequestCookies.ContainsKey(SessionCookieName) || entity.Server.IsBrowser();
        }
    }
}