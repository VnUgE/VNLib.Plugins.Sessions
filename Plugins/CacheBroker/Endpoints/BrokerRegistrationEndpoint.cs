﻿/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: CacheBroker
* File: BrokerRegistrationEndpoint.cs 
*
* BrokerRegistrationEndpoint.cs is part of CacheBroker which is part of the larger 
* VNLib collection of libraries and utilities.
*
* CacheBroker is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* CacheBroker is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with CacheBroker. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

using RestSharp;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Logging;
using VNLib.Utils.Extensions;
using VNLib.Hashing.IdentityUtility;
using VNLib.Plugins.Essentials;
using VNLib.Plugins.Essentials.Endpoints;
using VNLib.Plugins.Essentials.Extensions;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Extensions.Loading.Events;
using VNLib.Net.Rest.Client;

#nullable enable

namespace VNLib.Plugins.Cache.Broker.Endpoints
{
    [ConfigurationName("broker_endpoint")]
    public sealed class BrokerRegistrationEndpoint : ResourceEndpointBase
    {
        const string HEARTBEAT_PATH = "/heartbeat";

        private static readonly RestClientPool ClientPool = new(10,new RestClientOptions()
        {
            Encoding = Encoding.UTF8,
            FollowRedirects = false,
            MaxTimeout = 10 * 1000,
            ThrowOnAnyError = true
        }, null);

        private static readonly HashAlgorithmName SignatureHashAlg = HashAlgorithmName.SHA384;
        //using the es384 algorithm for signing
        private static readonly ECCurve DefaultCurve = ECCurve.CreateFromFriendlyName("secp384r1");

        private static readonly IReadOnlyDictionary<string, string> BrokerJwtHeader = new Dictionary<string, string>()
        {
            { "alg","ES384" },
            { "typ", "JWT"}
        };

        private class ActiveServer
        {
            [JsonIgnore]
            public IPAddress? ServerIp { get; set; }
            [JsonPropertyName("address")]
            public Uri? Address { get; set; }
            [JsonPropertyName("server_id")]
            public string? ServerId { get; init; }
            [JsonPropertyName("ip_address")]
            public string? Ip => ServerIp?.ToString();
            [JsonIgnore]
            public string? Token { get; init; }
        }


        private readonly object ListLock;
        private readonly Dictionary<string, ActiveServer> ActiveServers;

        private readonly Task<byte[]> CachePubKey;
        private readonly Task<byte[]> ClientPubKey;
        private readonly Task<byte[]> BrokerPrivateKey;

        protected override ProtectionSettings EndpointProtectionSettings { get; }

        public BrokerRegistrationEndpoint(PluginBase plugin, IReadOnlyDictionary<string, JsonElement> config)
        {
            string? path = config["path"].GetString();
           
            //Get the keys from the vault
            BrokerPrivateKey = plugin.TryGetSecretAsync("broker_private_key").ContinueWith((Task<string?> secret) =>
            {
                _ = secret.Result ?? throw new InvalidOperationException("Broker private key not found in vault");
                return Convert.FromBase64String(secret.Result);
            });

            CachePubKey = plugin.TryGetSecretAsync("cache_public_key").ContinueWith((Task<string?> secret) =>
            {
                _ = secret.Result ?? throw new InvalidOperationException("Cache public key not found in vault");
                return Convert.FromBase64String(secret.Result);
            });

            ClientPubKey = plugin.TryGetSecretAsync("client_public_key").ContinueWith((Task<string?> secret) =>
            {
                _ = secret.Result ?? throw new InvalidOperationException("Client public key not found in vault");
                return Convert.FromBase64String(secret.Result);
            });


            InitPathAndLog(path, plugin.Log);

            //Loosen up protection settings since this endpoint is not desinged for browsers or sessions
            EndpointProtectionSettings = new()
            {
                SessionsRequired = false,
                BrowsersOnly = false,
                CrossSiteDenied = false,
            };

            ListLock = new();
            ActiveServers = new();
        }


        protected override async ValueTask<VfReturnType> PostAsync(HttpEntity entity)
        {
            //Parse jwt
            using JsonWebToken jwt = await entity.ParseFileAsAsync(ParseJwtAsync) ?? throw new Exception("Invalid JWT");
            //Verify with the client's pub key
            using (ECDsa alg = ECDsa.Create(DefaultCurve))
            {
                alg.ImportSubjectPublicKeyInfo(ClientPubKey.Result, out _);
                //Verify with client public key
                if (!jwt.Verify(alg, in SignatureHashAlg))
                {
                    entity.CloseResponse(HttpStatusCode.Unauthorized);
                    return VfReturnType.VirtualSkip;
                }
            }
            try
            {
                //Get all active servers
                ActiveServer[] servers;
                lock (ListLock)
                {
                    servers = ActiveServers.Values.ToArray();
                }

                //Create response payload with list of active servers and sign it
                using JsonWebToken response = new();
                response.WriteHeader(BrokerJwtHeader);
                response.InitPayloadClaim(1)
                    .AddClaim("servers", servers)
                    .CommitClaims();

                //Sign the jwt using the broker key
                using(ECDsa alg = ECDsa.Create(DefaultCurve))
                {
                    alg.ImportPkcs8PrivateKey(BrokerPrivateKey.Result, out _);

                    response.Sign(alg, in SignatureHashAlg, 128);
                }
                
                //Alloc output buffer
                int bufSize = response.ByteSize * 2;
                
                using UnsafeMemoryHandle<char> charBuf = Memory.UnsafeAlloc<char>(bufSize, true);

                //compile jwt
                ERRNO count = response.Compile(charBuf);
                
                entity.CloseResponse(HttpStatusCode.OK, ContentType.Text, charBuf.Span[..(int)count]);
                return VfReturnType.VirtualSkip;
            }
            catch (KeyNotFoundException)
            {
                entity.CloseResponse(HttpStatusCode.UnprocessableEntity);
                return VfReturnType.VirtualSkip;
            }
        }

        /*
         * Server's call the put method to register or update their registration
         * for availability
         */

        private static async ValueTask<JsonWebToken?> ParseJwtAsync(Stream inputStream)
        {
            //get a buffer to store data in
            using VnMemoryStream buffer = new();
            //Copy input stream to buffer
            await inputStream.CopyToAsync(buffer, 4096, Memory.Shared);
            //Parse jwt
            return JsonWebToken.ParseRaw(buffer.AsSpan());
        }

        protected override async ValueTask<VfReturnType> PutAsync(HttpEntity entity)
        {
            //Parse jwt
            using JsonWebToken? jwt = await entity.ParseFileAsAsync(ParseJwtAsync);
            //Verify with the cache server's pub key
            using (ECDsa alg = ECDsa.Create(DefaultCurve))
            {
                alg.ImportSubjectPublicKeyInfo(CachePubKey.Result, out _);
                //Verify the jwt
                if (!jwt.Verify(alg, in SignatureHashAlg))
                {
                    entity.CloseResponse(HttpStatusCode.Unauthorized);
                    return VfReturnType.VirtualSkip;
                }
            }
            
            try
            {
                
                //Get message body
                using JsonDocument requestBody = jwt.GetPayload();
                
                //Get request keys
                string? serverId = requestBody.RootElement.GetProperty("server_id").GetString();
                string? hostname = requestBody.RootElement.GetProperty("address").GetString();
                string? token = requestBody.RootElement.GetProperty("token").GetString();

                if (string.IsNullOrWhiteSpace(serverId) || string.IsNullOrWhiteSpace(hostname))
                {
                    entity.CloseResponse(HttpStatusCode.UnprocessableEntity);
                    return VfReturnType.VirtualSkip;
                }
                
                //Build the hostname uri
                Uri serverUri = new(hostname);
                
                //Check hostname
                if (Uri.CheckHostName(serverUri.DnsSafeHost) != UriHostNameType.Dns)
                {
                    entity.CloseResponse(HttpStatusCode.UnprocessableEntity);
                    return VfReturnType.VirtualSkip;
                }
                
                //Check dns-ip resolution to the current connection if not local connection
                if (!entity.Server.IsLoopBack())
                {
                    //Resolve the ip address of the server's hostname to make sure its ip-address matches
                    IPHostEntry remoteHost = await Dns.GetHostEntryAsync(serverUri.DnsSafeHost);
                    //See if the dns lookup resolved the same ip address that connected to the server
                    bool isAddressMatch = (from addr in remoteHost.AddressList
                                           where addr.Equals(entity.TrustedRemoteIp)
                                           select addr)
                                          .Any();
                    //If the lookup fails, exit with forbidden
                    if (!isAddressMatch)
                    {
                        entity.CloseResponse(HttpStatusCode.Forbidden);
                        return VfReturnType.VirtualSkip;
                    }
                }
                
                //Server is allowed to be put into an active state
                ActiveServer server = new()
                {
                    Address = serverUri,
                    ServerIp = entity.TrustedRemoteIp,
                    ServerId = serverId,
                    Token = token
                };
                
                //Store/update active server
                lock (ListLock)
                {
                    ActiveServers[serverId] = server;
                }
                
                Log.Debug("Server {s}:{ip} added ", serverId, entity.TrustedRemoteIp);
                //Send the broker public key used to verify authenticating clients
                entity.CloseResponse(HttpStatusCode.OK);
                return VfReturnType.VirtualSkip;
            }
            catch (KeyNotFoundException)
            {
                entity.CloseResponse(HttpStatusCode.UnprocessableEntity);
                return VfReturnType.VirtualSkip;
            }
            catch (InvalidOperationException)
            {
                entity.CloseResponse(HttpStatusCode.BadRequest);
                return VfReturnType.VirtualSkip;
            }
            catch (UriFormatException)
            {
                entity.CloseResponse(HttpStatusCode.UnprocessableEntity);
                return VfReturnType.VirtualSkip;
            }
            catch (FormatException)
            {
                entity.CloseResponse(HttpStatusCode.BadRequest);
                return VfReturnType.BadRequest;
            }
            catch (JsonException)
            {
                entity.CloseResponse(HttpStatusCode.BadRequest);
                return VfReturnType.BadRequest;
            }
        }
       

        /*
         * Schedule heartbeat interval
         */
        [ConfigurableAsyncInterval("heartbeat_sec", IntervalResultionType.Seconds)]
        public async Task OnIntervalAsync(ILogProvider log, CancellationToken pluginExit)
        {
            ActiveServer[] servers;
            //Get the current list of active servers
            lock (ListLock)
            {
                servers = ActiveServers.Values.ToArray();
            }
            LinkedList<Task> all = new();
            //Run keeplaive request for all active servers
            foreach (ActiveServer server in servers)
            {
                all.AddLast(RunHeartbeatAsync(server));
            }
            //Wait for all to complete
            await Task.WhenAll(all);
        }

        private async Task RunHeartbeatAsync(ActiveServer server)
        {
            try
            {
                //build httpuri
                UriBuilder uri = new(server.Address!)
                {
                    Path = HEARTBEAT_PATH
                };
                string authMessage;
                //Init jwt for signing auth messages
                using (JsonWebToken jwt = new())
                {
                    jwt.WriteHeader(BrokerJwtHeader);
                    jwt.InitPayloadClaim()
                        .AddClaim("token", server.Token)
                        .CommitClaims();

                    //Sign the jwt using the broker key
                    using (ECDsa alg = ECDsa.Create(DefaultCurve))
                    {
                        alg.ImportPkcs8PrivateKey(BrokerPrivateKey.Result, out _);
                        //Sign with broker key
                        jwt.Sign(alg, in SignatureHashAlg, 128);
                    }
                    //compile
                    authMessage = jwt.Compile();
                }
                //Build keeplaive request
                RestRequest keepaliveRequest = new(uri.Uri, Method.Get);
                //Add authorization token
                keepaliveRequest.AddHeader("Authorization", authMessage);

                //Rent client from pool
                using ClientContract client = ClientPool.Lease();
                //Exec
                RestResponse response = await client.Resource.ExecuteAsync(keepaliveRequest);
                //If the response was successful, then keep it in the list, if the response fails, 
                if (response.IsSuccessful)
                {
                    return;
                }
                //Remove the server
            }
            catch (HttpRequestException re) when (re.InnerException is SocketException)
            {
                Log.Debug("Server {s} removed, failed to connect", server.ServerId);
            }
            catch (TimeoutException)
            {
                Log.Information("Server {s} removed from active list due to a connection timeout", server.ServerId);
            }
            catch (Exception ex)
            {
                Log.Information("Server {s} removed from active list due to failed heartbeat request", server.ServerId);
                Log.Debug(ex);
            }
            //Remove server from active list
            lock (ListLock)
            {
                _ = ActiveServers.Remove(server.ServerId!);
            }
        }
    }
}
