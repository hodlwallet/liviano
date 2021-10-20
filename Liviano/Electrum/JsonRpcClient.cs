//
// JsonRpcTcpClient.cs
//
// Copyright (c) 2019 Hodl Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Security;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;

using Liviano.Models;
using Liviano.Exceptions;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Liviano.Electrum
{
    public class JsonRpcClient
    {
        readonly TimeSpan DEFAULT_NETWORK_TIMEOUT = TimeSpan.FromSeconds(30.0);
        readonly TimeSpan DEFAULT_NETWORK_TIMEOUT_POLL = TimeSpan.FromSeconds(5.0);
        readonly int TCP_CLIENT_POLL_TIME_TO_WAIT = 5000; // Milliseconds for polling, 5 seconds.

        readonly Server server;

        readonly object @lock = new();

        IPAddress ipAddress;

        TcpClient tcpClient;
        SslStream sslStream;

        bool isSslPolling = false;
        bool readingStream = false;
        bool consumingQueue = false;

        ConcurrentDictionary<string, string> results;
        ConcurrentQueue<string> queue;

        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;

        int port;
        public string Host { get; private set; }

        public CancellationTokenSource Cts;

        public Task PollSslClientTask;
        public Task ConsumeMessagesTask;
        public Task ConsumeRequestTask;

        public JsonRpcClient(Server server)
        {
            this.server = server;

            Cts ??= new();

            InitQueue();
        }

        void InitQueue()
        {
            var initialCapacity = 101;
            var numProcs = Environment.ProcessorCount;
            var concurrencyLevel = numProcs * 2;

            results = new ConcurrentDictionary<string, string>(initialCapacity, concurrencyLevel);
            queue = new ConcurrentQueue<string>();
        }

        static async Task<IPAddress> ResolveAsync(string hostName)
        {
            var hostEntry = await Dns.GetHostEntryAsync(hostName);
            return hostEntry.AddressList.ToArray().First();
        }

        async Task<IPAddress> ResolveHost(string hostName)
        {
            try
            {
                var ipAddress = await ResolveAsync(hostName);
                if (ipAddress == null) throw new TimeoutException($"Timed out connecting to {hostName}:{port}");

                return ipAddress;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ResolveHost] Error: ", ex.Message);

                throw new ElectrumException($"DNS host entry lookup resulted in no records for {hostName}\n{ex.Message}");
            }
        }

        TcpClient Connect()
        {
            var tcpClient = new TcpClient(ipAddress.AddressFamily)
            {
                SendTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds),
                ReceiveTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds),
            };

            var isConnected = tcpClient.ConnectAsync(ipAddress, port).Wait(DEFAULT_NETWORK_TIMEOUT);

            if (!isConnected)
            {
                throw new ElectrumException("Server is unresponsive.");
            }

            return tcpClient;
        }

        public void Disconnect()
        {
            lock (@lock)
            {
                Cts.Cancel();

                tcpClient.Dispose();
                tcpClient = null;

                sslStream.Dispose();
                sslStream = null;

                results.Clear();
                queue.Clear();

                isSslPolling = false;
                consumingQueue = false;
            }

            OnDisconnected?.Invoke(this, null);
        }

        async Task<string> RequestInternal(string request)
        {
            Debug.WriteLine($"[RequestInternal] Sending request: {request}");

            // TODO add usecured tcp streams support back

            return await RequestInternalSsl(request);
        }

        async Task<string> RequestInternalSsl(string request)
        {
            tcpClient ??= Connect();
            sslStream ??= SslTcpClient.GetSslStream(tcpClient, Host);

            var json = JObject.Parse(request);
            var requestId = (string)json.GetValue("id");

            PollSslClient();

            ConsumeMessages();

            EnqueueMessage(requestId, request);

            ConsumeRequests();

            return await GetResult(requestId);
        }

        public async Task<string> Request(string request)
        {
            Host = server.Domain;
            ipAddress = ResolveHost(server.Domain).Result;
            port = server.PrivatePort.Value;

            Debug.WriteLine(
                $"[Request] Server: {Host}:{port} ({server.Version}) Request: {request}"
            );

            var result = await RequestInternal(request);

            if (result == null) throw new ElectrumException("Timeout when trying to communicate with server");

            return result;
        }

        public async Task Subscribe(string request, Action<string> resultCallback, Action<string> notificationCallback)
        {
            Host = server.Domain;
            ipAddress = ResolveHost(server.Domain).Result;
            port = server.PrivatePort.Value;

            tcpClient ??= Connect();
            sslStream ??= SslTcpClient.GetSslStream(tcpClient, Host);

            var json = JObject.Parse(request);
            var requestId = (string)json.GetValue("id");
            var method = (string)json.GetValue("method");

            PollSslClient();

            ConsumeMessages();

            EnqueueMessage(requestId, request);

            ConsumeRequests();

            resultCallback(await GetResult(requestId));

            if (string.Equals(method, "blockchain.scripthash.subscribe"))
            {
                var @params = (JArray)json.GetValue("params");
                var scripthash = (string)@params[0];

                results[scripthash] = null;
                requestId = scripthash;
            }
            else if (string.Equals(method, "blockchain.headers.subscribe"))
            {
                results["blockchain.headers.subscribe"] = null;
                requestId = "blockchain.headers.subscribe";
            }
            else
            {
                throw new WalletException("Invalid subscription");
            }

            await Task.Factory.StartNew(
                o => CallbackOnResult(requestId, notificationCallback),
                TaskCreationOptions.LongRunning,
                Cts.Token
            );
        }

        void PollSslClient()
        {
            if (isSslPolling) return;

            isSslPolling = true;

            PollSslClientTask = Task.Run(async () =>
            {
                var ping = new Ping();
                var pingOptions = new PingOptions(64, true)
                {
                    DontFragment = true
                };
                var pingData = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                var pingTimeout = TCP_CLIENT_POLL_TIME_TO_WAIT;
                var pingBuffer = Encoding.ASCII.GetBytes(pingData);

                bool isConnected = false;
                while (true)
                {
                    bool initialIsConnected = isConnected;

                    if (NetworkInterface.GetIsNetworkAvailable())
                    {
                        // Ping the hostname first
                        var pingTask = Task.Run(() =>
                        {
                            var pingReply = ping.Send(Host, pingTimeout, pingBuffer, pingOptions);

                            if (pingReply.Status != IPStatus.Success)
                            {
                                Debug.WriteLine("[PollSslClient] Ping failed.");
                                isConnected = false;
                            }
                        }, Cts.Token);

                        if (await Task.WhenAny(pingTask, Task.Delay(TimeSpan.FromMilliseconds(TCP_CLIENT_POLL_TIME_TO_WAIT))) != pingTask)
                        {
                            Debug.WriteLine("[PollSslClient] Disconnected, ping task timeout!");
                            isConnected = false;
                        }
                        else
                        {
                            var tcpPollTask = Task.Run(() =>
                            {
                                // Now we test if the tcpclient can connect
                                using var pollTcpClient = new TcpClient(ipAddress.AddressFamily)
                                {
                                    SendTimeout = Convert.ToInt32(TCP_CLIENT_POLL_TIME_TO_WAIT),
                                    ReceiveTimeout = Convert.ToInt32(TCP_CLIENT_POLL_TIME_TO_WAIT)
                                };

                                isConnected = pollTcpClient.ConnectAsync(ipAddress, port).Wait(TimeSpan.FromMilliseconds(TCP_CLIENT_POLL_TIME_TO_WAIT));
                                if (!isConnected)
                                {
                                    Debug.WriteLine("[PollSslClient] Stream is disconnected.");
                                }
                            }, Cts.Token);

                            if (await Task.WhenAny(tcpPollTask, Task.Delay(TimeSpan.FromMilliseconds(TCP_CLIENT_POLL_TIME_TO_WAIT))) != tcpPollTask)
                            {
                                Debug.WriteLine("[PollSslClient] Disconnected, ping task timeout!");
                                isConnected = false;
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[PollSslClient] Network not available");

                        isConnected = false;
                    }

                    if (isConnected)
                    {
                        if (!initialIsConnected) OnConnected?.Invoke(this, null);
                    }
                    else
                    {
                        Debug.WriteLine("[PollSslClient] Disconnected!");

                        if (initialIsConnected) OnDisconnected?.Invoke(this, null);
                    }

                    await Task.Delay(TCP_CLIENT_POLL_TIME_TO_WAIT);
                }
            }, Cts.Token);
        }

        async Task<string> GetResult(string requestId)
        {
            var loopDelay = 3000;

            // Wait for new messages' responses
            while (!results.ContainsKey(requestId) || results[requestId] == null)
                await Task.Delay(loopDelay);

            // Now the result is ready
            var res = results[requestId];
            results[requestId] = null;
            results.TryRemove(requestId, out var _);

            return res;
        }

        async Task CallbackOnResult(string requestId, Action<string> callback)
        {
            Debug.WriteLine($"[CallbackOnResult] Callback for requestId: {requestId}");

            callback(await GetResult(requestId));

            await CallbackOnResult(requestId, callback);
        }

        void ConsumeRequests()
        {
            if (consumingQueue) return;

            consumingQueue = true;

            ConsumeMessagesTask = Task.Run(async () =>
            {
                var loopDelay = 3000;
                try
                {
                    while (true)
                    {
                        while (!queue.IsEmpty)
                        {
                            queue.TryDequeue(out string req);

                            var json = JsonConvert.DeserializeObject<JObject>(req);
                            var requestId = (string)json.GetValue("id");
                            var bytes = Encoding.UTF8.GetBytes(req + "\n");

                            await sslStream.WriteAsync(bytes.AsMemory(0, bytes.Length));
                            await sslStream.FlushAsync();
                        }

                        await Task.Delay(loopDelay);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[ConsumeQueue] Error: {e.StackTrace}");

                    consumingQueue = false;
                }

                consumingQueue = false;
                ConsumeRequests();
            }, Cts.Token);
        }

        void EnqueueMessage(string requestId, string request)
        {
            if (queue.Any(o => string.Equals(o, requestId))) return;

            results[requestId] = null;
            queue.Enqueue(request);
        }

        /// <summary>
        /// Consume messages from the stream
        /// </summary>
        void ConsumeMessages()
        {
            if (readingStream) return;

            readingStream = true;

            ConsumeMessagesTask = Task.Run(async () =>
            {
                using var stream = sslStream;

                try
                {
                    await SslTcpClient.ReadMessagesFrom(stream, (msgs) =>
                    {
                        foreach (var msg in msgs.Split('\n'))
                        {
                            if (string.IsNullOrEmpty(msg)) continue;

                            var json = JsonConvert.DeserializeObject<JObject>(msg);

                            if (json.ContainsKey("method")) // A subscription notification
                            {
                                var @params = json.GetValue("params");
                                var method = (string)json.GetValue("method");

                                if (string.Equals(method, "blockchain.scripthash.subscribe"))
                                {
                                    var scripthash = (string)@params[0];
                                    var status = (string)@params[1];

                                    // FIXME We need to wait for empty result,
                                    // it should be empty but sometimes this fails
                                    //await WaitForEmptyResult(scripthash);

                                    results[scripthash] = status;
                                }
                                else if (string.Equals(method, "blockchain.headers.subscribe"))
                                {
                                    var newHeader = (JObject)@params[0];

                                    // See above
                                    //await WaitForEmptyResult("blockchain.headers.subscribe");

                                    results["blockchain.headers.subscribe"] = newHeader.ToString(Formatting.None);
                                }
                            }
                            else
                            {
                                var requestId = (string)json.GetValue("id");

                                // See above
                                //await WaitForEmptyResult(requestId);

                                results[requestId] = msg;
                            }
                        }
                    });
                }
                catch (Exception e)
                {
                    if (Cts.IsCancellationRequested || sslStream == null)
                    {
                        Debug.WriteLine("[ConsumeMessages] Cancelled.");

                        readingStream = false;

                        return;
                    }

                    Debug.WriteLine($"[ConsumeMessages] Error: {e.Message}, Stracktrace: {e.StackTrace}");

                    readingStream = false;

                    ConsumeMessages();
                }
            }, Cts.Token);
        }

#pragma warning disable IDE0051 // Remove unused private members
        async Task WaitForEmptyResult(string requestId)
#pragma warning restore IDE0051 // Remove unused private members
        {
            var loopDelay = 10;
            results.TryGetValue(requestId, out string val);
            while (!string.IsNullOrEmpty(val))
            {
                if (Cts.IsCancellationRequested) return;

                await Task.Delay(loopDelay);
            }
        }
    }
}
