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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;

using Liviano.Exceptions;
using Liviano.Models;

namespace Liviano.Electrum
{
    public class JsonRpcClient
    {
        readonly TimeSpan DEFAULT_NETWORK_TIMEOUT = TimeSpan.FromSeconds(30.0);
        readonly int TCP_CLIENT_POLL_TIME_TO_WAIT = 5000; // Milliseconds for polling, 5 seconds.

        readonly Server server;

        readonly object @lock = new();

        IPAddress ipAddress;

        TcpClient tcpClient;
        SslStream sslStream;

        bool isSslPolling = false;
        bool readingStream = false;
        bool consumingQueue = false;

        ConcurrentDictionary<string, string> results = new(101, Environment.ProcessorCount * 10);
        ConcurrentQueue<string> queue = new ();

        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;

        public event EventHandler<string> OnRequestFailed;
        public event EventHandler<string> OnSubscriptionFailed;

        int port;
        public string Host { get; private set; }

        public CancellationTokenSource Cts = new();

        public JsonRpcClient(Server server)
        {
            this.server = server;
        }

        public void StartTasks()
        {
            Observable
                .Start(() =>
                {
                    ConsumeMessages();
                    ConsumeRequests();
                    PollSslClient();
                }, RxApp.TaskpoolScheduler);
        }

        static async Task<IPAddress> ResolveAsync(string hostName)
        {
            var hostEntry = await Dns.GetHostEntryAsync(hostName);

            return hostEntry.AddressList.First();
        }

        async Task<IPAddress> ResolveHost(string hostName)
        {
            try
            {
                var ipAddress = await ResolveAsync(hostName);
                if (ipAddress == null) throw new ElectrumException($"Timed out connecting to {hostName}:{port}");

                return ipAddress;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ResolveHost] Error: ", ex.Message);

                throw new ElectrumException($"DNS host entry lookup resulted in no records for {hostName}\n{ex.Message}");
            }
        }

        public TcpClient Connect()
        {
            var tcpClient = new TcpClient(ipAddress.AddressFamily)
            {
                SendTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds),
                ReceiveTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds),
            };

            var isConnected = tcpClient.ConnectAsync(ipAddress, port).Wait(DEFAULT_NETWORK_TIMEOUT);

            if (!isConnected) throw new ElectrumException("Server is unresponsive.");

            return tcpClient;
        }

        public void Disconnect()
        {
            lock (@lock)
            {
                Cts.Cancel();

                if (tcpClient is not null)
                {
                    tcpClient.Dispose();
                    tcpClient = null;
                }

                if (sslStream is not null)
                {
                    sslStream.Dispose();
                    sslStream = null;
                }

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

            // TODO add usecured tcp streams (http) support back,
            // this will always be ssl for now

            return await RequestInternalSsl(request);
        }

        async Task<string> RequestInternalSsl(string request)
        {
            tcpClient ??= Connect();
            sslStream ??= SslTcpClient.GetSslStream(tcpClient, Host);

            StartTasks();

            var json = JObject.Parse(request);
            var requestId = (string)json.GetValue("id");
            var requestMethod = (string)json.GetValue("method");

            // Since transactions get fetched many times,
            // we do this method that catches the hexes we get
            if (string.Equals(requestMethod, "blockchain.transaction.get"))
                return await DoGetTransactionHex(request);

            EnqueueMessage(requestId, request);

            return await GetResult(requestId);
        }

        ConcurrentDictionary<string, string> TransactionHexCache { get; set; } = new(101, Environment.ProcessorCount * 10);
        async Task<string> DoGetTransactionHex(string request)
        {
            if (TransactionHexCache.ContainsKey(request)) return TransactionHexCache[request];

            using var tcpClientLocal = Connect();
            using var stream = SslTcpClient.GetSslStream(tcpClientLocal, Host);
            var data = Encoding.UTF8.GetBytes(request + "\n");

            await stream.WriteAsync(data.AsMemory(0, data.Length));
            await stream.FlushAsync();

            byte[] buffer = new byte[2048];
            StringBuilder messageData = new();

            while (true)
            {
                int bytes = await stream.ReadAsync(buffer);

                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];

                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);

                if (bytes < 2048) break;
            }

            TransactionHexCache[request] = messageData.ToString();

            return TransactionHexCache[request];
        }

        public async Task<string> Request(string request)
        {
            Host ??= server.Domain;
            ipAddress ??= ResolveHost(server.Domain).Result;
            port = server.PrivatePort.Value;

            Debug.WriteLine(
                $"[Request] Server: {Host}:{port} ({server.Version}) Request: {request}"
            );

            string result = null;
            try
            {
                result = await RequestInternal(request);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error: {e}");

                OnRequestFailed?.Invoke(this, request);
            }

            if (result == null) 
            {
                Debug.WriteLine("Timeout when trying to communicate with server");

                OnRequestFailed?.Invoke(this, request);
            }

            return result;
        }

        public async Task Subscribe(string request, Action<string> resultCallback, Action<string> notificationCallback)
        {
            Host = server.Domain;
            ipAddress = ResolveHost(server.Domain).Result;
            port = server.PrivatePort.Value;

            tcpClient ??= Connect();
            sslStream ??= SslTcpClient.GetSslStream(tcpClient, Host);

            StartTasks();

            var json = JObject.Parse(request);
            var requestId = (string)json.GetValue("id");
            var method = (string)json.GetValue("method");

            EnqueueMessage(requestId, request);

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
                Debug.WriteLine("[Subscribe] Invalid subscription");

                OnSubscriptionFailed?.Invoke(this, requestId);

                return;
            }

            Observable.Start(async () =>
            {
                try
                {
                    await CallbackOnResult(requestId, notificationCallback);

                    Debug.WriteLine($"[Subscribe] Subscription finished, but shouldn't");
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[Subscribe] Subscription failed. Error: {e}");
                }
                finally
                {
                    OnSubscriptionFailed?.Invoke(this, requestId);
                }
            }, RxApp.TaskpoolScheduler).Subscribe(Cts.Token);
        }

        void PollSslClient()
        {
            if (isSslPolling) return;

            isSslPolling = true;

            Observable.Start(() =>
            {
                var ping = new Ping();
                var pingOptions = new PingOptions(64, true);
                var pingData = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                var pingTimeout = TCP_CLIENT_POLL_TIME_TO_WAIT;
                var pingBuffer = Encoding.ASCII.GetBytes(pingData);

                bool isConnected = false;

                Observable
                    .Interval(TimeSpan.FromMilliseconds(TCP_CLIENT_POLL_TIME_TO_WAIT), RxApp.TaskpoolScheduler)
                    .Subscribe(_ =>
                {
                    bool initialIsConnected = isConnected;

                    if (!NetworkInterface.GetIsNetworkAvailable())
                    {
                        isConnected = false;

                        if (initialIsConnected) OnDisconnected?.Invoke(this, null);

                        return;
                    }

                    if (ipAddress is not null)
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

                        if (isConnected)
                        {
                            if (!initialIsConnected) OnConnected?.Invoke(this, null);
                        }
                        else
                        {
                            Debug.WriteLine("[PollSslClient] Disconnected!");

                            if (initialIsConnected) OnDisconnected?.Invoke(this, null);
                        }

                        return;
                    }

                    var pingReply = ping.Send(Host, pingTimeout, pingBuffer, pingOptions);

                    if (pingReply.Status != IPStatus.Success)
                    {
                        Debug.WriteLine("[PollSslClient] Ping failed.");

                        isConnected = false;
                    }
                }, Cts.Token);
            }).Subscribe(Cts.Token);
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

            Observable.Start(async () =>
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
                    Debug.WriteLine($"[ConsumeRequests] Error: {e.StackTrace}");
                }

                consumingQueue = false;
                ConsumeRequests();
            }).Subscribe(Cts.Token);
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

            Observable.Start(async () =>
            {
                try
                {
                    using var stream = sslStream;

                    await SslTcpClient.ReadMessagesFrom(stream, (msgs) =>
                    {
                        foreach (var msg in msgs.Split('\n'))
                        {
                            if (string.IsNullOrEmpty(msg)) continue;

                            string saneMsg = msg;
                            JObject json = null;
                            try
                            {
                                json = JsonConvert.DeserializeObject<JObject>(saneMsg);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ConsumeMessages] Error: {ex.StackTrace}, trying to sanitize");

                                saneMsg = SanitizeMsg(msg);
                                json = JsonConvert.DeserializeObject<JObject>(saneMsg);
                            }

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
                                    // await WaitForEmptyResult(scripthash);

                                    results[scripthash] = status;
                                }
                                else if (string.Equals(method, "blockchain.headers.subscribe"))
                                {
                                    var newHeader = (JObject)@params[0];

                                    // See above
                                    // await WaitForEmptyResult("blockchain.headers.subscribe");

                                    results["blockchain.headers.subscribe"] = newHeader.ToString(Formatting.None);
                                }
                            }
                            else
                            {
                                var requestId = (string)json.GetValue("id");

                                // See above
                                // await WaitForEmptyResult(requestId);

                                results[requestId] = saneMsg;
                            }
                        }
                    });
                }
                catch (Exception e)
                {
                    if (Cts.IsCancellationRequested || sslStream is null)
                    {
                        Debug.WriteLine("[ConsumeMessages] Cancelled.");

                        readingStream = false;

                        return;
                    }

                    Debug.WriteLine($"[ConsumeMessages] Error: {e.Message}, Stracktrace: {e.StackTrace}");

                    tcpClient = Connect();
                    sslStream = SslTcpClient.GetSslStream(tcpClient, Host);

                    readingStream = false;

                    ConsumeMessages();
                }
            }).Subscribe(Cts.Token);
        }

        // FIXME: This is a hack, this method is to fix the problem of a msg not being parsed correctly due to an error
        // in the ssl stream or the electrum server when calling the blockchain.scripthash.transaction.get
        // which is a very special call in the electrumx spec. This code asumes this error but should work with
        // all the other json messages
        static string SanitizeMsg(string msg)
        {
            var idx = msg.LastIndexOf("{\"jsonrpc\"");

            if (idx > 0) return msg[idx..];
            else return msg;
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
