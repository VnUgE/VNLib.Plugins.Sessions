﻿/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: ObjectCacheServer
* File: BrokerHeartBeat.cs 
*
* BrokerHeartBeat.cs is part of ObjectCacheServer which is part of the larger 
* VNLib collection of libraries and utilities.
*
* ObjectCacheServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* ObjectCacheServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Net;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;

using VNLib.Data.Caching.Extensions;
using VNLib.Hashing.IdentityUtility;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Extensions.Loading;

namespace VNLib.Plugins.Essentials.Sessions.Server.Endpoints
{
    internal class BrokerHeartBeat : ResourceEndpointBase
    {
        public override string Path => "/heartbeat";

        private readonly Func<string> Token;
        private readonly ManualResetEvent KeepaliveSet;
        private readonly Task<IPAddress[]> BrokerIpList;
        private readonly PluginBase Pbase;

        protected override ProtectionSettings EndpointProtectionSettings { get; }

        public BrokerHeartBeat(Func<string> token, ManualResetEvent keepaliveSet, Uri brokerUri, PluginBase pbase)
        {
            Token = token;
            KeepaliveSet = keepaliveSet;
            BrokerIpList = Dns.GetHostAddressesAsync(brokerUri.DnsSafeHost);
            
            this.Pbase = pbase;

            EndpointProtectionSettings = new()
            {
                BrowsersOnly = false,
                SessionsRequired = false,
                VerifySessionCors = false,
            };
        }

        private async Task<byte[]> GetBrokerPubAsync()
        {
            string? brokerPubKey = await Pbase.TryGetSecretAsync("broker_public_key") ?? throw new KeyNotFoundException("Missing required secret : broker_public_key");

            return Convert.FromBase64String(brokerPubKey);
        }

        protected override async ValueTask<VfReturnType> GetAsync(HttpEntity entity)
        {
            //If-not loopback then verify server address
            if (!entity.Server.IsLoopBack())
            {
                //Load and verify the broker's ip address matches with an address we have stored
                IPAddress[] addresses = await BrokerIpList;
                if (!addresses.Contains(entity.TrustedRemoteIp))
                {
                    //Token invalid
                    entity.CloseResponse(HttpStatusCode.Forbidden);
                    return VfReturnType.VirtualSkip;
                }
            }
            //Get the authorization jwt
            string? jwtAuth = entity.Server.Headers[HttpRequestHeader.Authorization];
            if (string.IsNullOrWhiteSpace(jwtAuth))
            {
                //Token invalid
                entity.CloseResponse(HttpStatusCode.Forbidden);
                return VfReturnType.VirtualSkip;
            }
            //Parse the jwt
            using JsonWebToken jwt = JsonWebToken.Parse(jwtAuth);
            //Init signature alg
            using (ECDsa alg = ECDsa.Create(FBMDataCacheExtensions.CacheCurve))
            {
                //Get pub key
                byte[] key = await GetBrokerPubAsync();

                alg.ImportSubjectPublicKeyInfo(key, out _);
                //Verify the jwt
                if (!jwt.Verify(alg, FBMDataCacheExtensions.CacheJwtAlgorithm))
                {
                    //Token invalid
                    entity.CloseResponse(HttpStatusCode.Forbidden);
                    return VfReturnType.VirtualSkip;
                }
            }
            string? auth;
            //Recover the auth token from the jwt
            using (JsonDocument doc = jwt.GetPayload())
            {
                auth = doc.RootElement.GetProperty("token").GetString();
            }
            //Verify token
            if(Token().Equals(auth, StringComparison.Ordinal))
            {
                //Signal keepalive
                KeepaliveSet.Set();
                entity.CloseResponse(HttpStatusCode.OK);
                return VfReturnType.VirtualSkip;
            }
            //Token invalid
            entity.CloseResponse(HttpStatusCode.Forbidden);
            return VfReturnType.VirtualSkip;
        }
    }
}
