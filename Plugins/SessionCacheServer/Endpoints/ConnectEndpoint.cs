using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Collections.Concurrent;

using VNLib.Utils.Async;
using VNLib.Utils.Logging;
using VNLib.Hashing.IdentityUtility;
using VNLib.Net.Messaging.FBM;
using VNLib.Net.Messaging.FBM.Client;
using VNLib.Net.Messaging.FBM.Server;
using VNLib.Data.Caching.Extensions;
using VNLib.Data.Caching.ObjectCache;
using VNLib.Plugins.Extensions.Loading;
using VNLib.Plugins.Essentials.Extensions;


namespace VNLib.Plugins.Essentials.Sessions.Server
{
    class ConnectEndpoint : ResourceEndpointBase
    {

        const int MAX_RECV_BUF_SIZE = 1000 * 1024;
        const int MIN_RECV_BUF_SIZE = 8 * 1024;
        const int MAX_HEAD_BUF_SIZE = 2048;
        const int MIN_MESSAGE_SIZE = 10 * 1024;
        const int MAX_MESSAGE_SIZE = 1000 * 1024;
        const int MIN_HEAD_BUF_SIZE = 128;
        const int MAX_EVENT_QUEUE_SIZE = 10000;
        const int MAX_RESPONSE_BUFFER_SIZE = 10 * 1024;

        private static readonly Encoding FBMHeaderEncoding = Helpers.DefaultEncoding;

        private readonly ObjectCacheStore Store;
        private readonly PluginBase Pbase;

        private readonly ConcurrentDictionary<string, AsyncQueue<ChangeEvent>> StatefulEventQueue;

        private uint _connectedClients;

        public uint ConnectedClients => _connectedClients;

        protected override ProtectionSettings EndpointProtectionSettings { get; }

        public ConnectEndpoint(string path, ObjectCacheStore store, PluginBase pbase)
        {
            InitPathAndLog(path, pbase.Log);
            Store = store;//Load client public key to verify signed messages
            Pbase = pbase;


            StatefulEventQueue = new(StringComparer.OrdinalIgnoreCase);
            //Start the queue worker
            _ = ChangeWorkerAsync().ConfigureAwait(false);

            //Loosen up protection settings
            EndpointProtectionSettings = new()
            {
                BrowsersOnly = false,
                SessionsRequired = false,
                CrossSiteDenied = false
            };
        }

        private async Task<byte[]> GetClientPubAsync()
        {
            string? brokerPubKey = await Pbase.TryGetSecretAsync("client_public_key") ?? throw new KeyNotFoundException("Missing required secret : client_public_key");

            return Convert.FromBase64String(brokerPubKey);
        }

        private async Task ChangeWorkerAsync()
        {
            try
            {
                //Listen for changes
                while (true)
                {
                    ChangeEvent ev = await Store.EventQueue.DequeueAsync(Pbase.UnloadToken);
                    //Add event to queues
                    foreach (AsyncQueue<ChangeEvent> queue in StatefulEventQueue.Values)
                    {
                        if (!queue.TryEnque(ev))
                        {
                            Log.Debug("Listener queue has exeeded capacity, change events will be lost");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {}
            catch(Exception ex)
            {
                Log.Error(ex);
            }
        }

        private class WsUserState
        {
            public int RecvBufferSize { get; init; }
            public int MaxHeaderBufferSize { get; init; }
            public int MaxMessageSize { get; init; }
            public int MaxResponseBufferSize { get; init; } 
            public AsyncQueue<ChangeEvent>? SyncQueue { get; init; }
        }

        protected override async ValueTask<VfReturnType> WebsocketRequestedAsync(HttpEntity entity)
        {
            try
            {
                //Parse jwt from authoriation
                string? jwtAuth = entity.Server.Headers[HttpRequestHeader.Authorization];
                if (string.IsNullOrWhiteSpace(jwtAuth))
                {
                    entity.CloseResponse(HttpStatusCode.Unauthorized);
                    return VfReturnType.VirtualSkip;
                }
                string? nodeId = null;
                //Parse jwt
                using (JsonWebToken jwt = JsonWebToken.Parse(jwtAuth))
                {
                    //Get the client public key
                    byte[] clientPub = await GetClientPubAsync();

                    //Init sig alg
                    using ECDsa sigAlg = ECDsa.Create(FBMDataCacheExtensions.CacheCurve);
                    //Import client pub key
                    sigAlg.ImportSubjectPublicKeyInfo(clientPub, out _);
                    //verify signature for client
                    if (!jwt.Verify(sigAlg, FBMDataCacheExtensions.CacheJwtAlgorithm))
                    {
                        entity.CloseResponse(HttpStatusCode.Unauthorized);
                        return VfReturnType.VirtualSkip;
                    }
                    //Recover json body
                    using JsonDocument doc = jwt.GetPayload();
                    if (doc.RootElement.TryGetProperty("server_id", out JsonElement servIdEl))
                    {
                        nodeId = servIdEl.GetString();
                    }
                }
                //Get query config suggestions from the client
                string recvBufCmd = entity.QueryArgs[FBMClient.REQ_RECV_BUF_QUERY_ARG];
                string maxHeaderCharCmd = entity.QueryArgs[FBMClient.REQ_HEAD_BUF_QUERY_ARG];
                string maxMessageSizeCmd = entity.QueryArgs[FBMClient.REQ_MAX_MESS_QUERY_ARG];
                //Parse recv buffer size
                int recvBufSize = int.TryParse(recvBufCmd, out int rbs) ? rbs : MIN_RECV_BUF_SIZE;
                int maxHeadBufSize = int.TryParse(maxHeaderCharCmd, out int hbs) ? hbs : MIN_HEAD_BUF_SIZE;
                int maxMessageSize = int.TryParse(maxMessageSizeCmd, out int mxs) ? mxs : MIN_MESSAGE_SIZE;
                AsyncQueue<ChangeEvent>? nodeQueue = null;
                //The connection may be a caching server node, so get its node-id
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    /*
                     * Store a new async queue, or get an old queue for the current node
                     * 
                     * We should use a bounded queue and disacard LRU items, we also know
                     * only a single writer is needed as the queue is processed on a single thread
                     * and change events may be processed on mutliple threads.
                    */

                    BoundedChannelOptions queueOptions = new(MAX_EVENT_QUEUE_SIZE)
                    {
                        AllowSynchronousContinuations = true,
                        SingleReader = false,
                        SingleWriter = true,
                        //Drop oldest item in queue if full
                        FullMode = BoundedChannelFullMode.DropOldest,
                    };

                    _ = StatefulEventQueue.TryAdd(nodeId, new(queueOptions));
                    //Get the queue
                    nodeQueue = StatefulEventQueue[nodeId];
                }
                //Init new ws state object and clamp the suggested buffer sizes
                WsUserState state = new()
                {
                    RecvBufferSize = Math.Clamp(recvBufSize, MIN_RECV_BUF_SIZE, MAX_RECV_BUF_SIZE),
                    MaxHeaderBufferSize = Math.Clamp(maxHeadBufSize, MIN_HEAD_BUF_SIZE, MAX_HEAD_BUF_SIZE),
                    MaxMessageSize = Math.Clamp(maxMessageSize, MIN_MESSAGE_SIZE, MAX_MESSAGE_SIZE),
                    MaxResponseBufferSize = Math.Min(maxMessageSize, MAX_RESPONSE_BUFFER_SIZE),
                    SyncQueue = nodeQueue
                };
                Log.Debug("Client recv buffer suggestion {recv}, header buffer size {head}, response buffer size {r}", recvBufCmd, maxHeaderCharCmd, state.MaxResponseBufferSize);
                //Accept socket and pass state object
                entity.AcceptWebSocket(WebsocketAcceptedAsync, state);
                return VfReturnType.VirtualSkip;
            }
            catch (KeyNotFoundException)
            {
                return VfReturnType.BadRequest;
            }
        }
        private async Task WebsocketAcceptedAsync(WebSocketSession wss)
        {
            //Inc connected count
            Interlocked.Increment(ref _connectedClients);
            //Register plugin exit token to cancel the connected socket
            CancellationTokenRegistration reg = Pbase.UnloadToken.Register(wss.CancelAll);
            try
            {
                WsUserState state = (wss.UserState as WsUserState)!;
                
                //Init listener args from request
                FBMListenerSessionParams args = new()
                {
                    MaxMessageSize = state.MaxMessageSize,
                    RecvBufferSize = state.RecvBufferSize,
                    ResponseBufferSize = state.MaxResponseBufferSize,
                    MaxHeaderBufferSize = state.MaxHeaderBufferSize,
                    HeaderEncoding = FBMHeaderEncoding,
                };

                //Listen for requests
                await Store.ListenAsync(wss, args, state.SyncQueue);
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Websocket connection was canceled");
                //Disconnect the socket
                await wss.CloseSocketOutputAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "unload", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Debug(ex);
            }
            finally
            {
                //Dec connected count
                Interlocked.Decrement(ref _connectedClients);
                //Unregister the 
                reg.Unregister();
            }
            Log.Debug("Server websocket exited");
        }
    }
}
