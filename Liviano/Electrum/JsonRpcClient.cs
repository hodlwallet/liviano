﻿//
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

        IPAddress ipAddress;

        TcpClient tcpClient;
        SslStream sslStream;

        [ThreadStatic] bool isSslPooling = false;

        [ThreadStatic] bool readingStream = false;
        bool consumingQueue = false;
        ConcurrentDictionary<string, string> results;
        ConcurrentQueue<string> queue;

        int port;
        public string Host { get; private set; }

        public JsonRpcClient(Server server)
        {
            this.server = server;

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

            _ = PollSslClient();

            _ = ConsumeMessages();

            EnqueueMessage(requestId, request);

            _ = ConsumeQueue();

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

        public async Task Subscribe(string request, Action<string> resultCallback, Action<string> notificationCallback, CancellationTokenSource cts = null)
        {
            Host = server.Domain;
            ipAddress = ResolveHost(server.Domain).Result;
            port = server.PrivatePort.Value;

            tcpClient ??= Connect();
            sslStream ??= SslTcpClient.GetSslStream(tcpClient, Host);

            if (cts is null) cts = new CancellationTokenSource();

            var ct = cts.Token;
            var json = JObject.Parse(request);
            var requestId = (string)json.GetValue("id");
            var method = (string)json.GetValue("method");

            _ = PollSslClient();

            _ = ConsumeMessages();

            EnqueueMessage(requestId, request);

            _ = ConsumeQueue();

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
                ct
            );
        }

        async Task PollSslClient()
        {
            if (isSslPooling) return;

            isSslPooling = true;
            var isConnected = false;
            while (true)
            {
                // First test if we can get the ip address in case of a disconnected client:
                var @ipAddress = GetIp

                // Now we test if the tcpclient can connect
                Console.WriteLine("****************************3WTF*****************");
                using var pollTcpClient = new TcpClient(ipAddress.AddressFamily)
                {
                    SendTimeout = Convert.ToInt32(1000),
                    ReceiveTimeout = Convert.ToInt32(1000)
                };
                Console.WriteLine("****************************2WTF*****************");
                
                isConnected = pollTcpClient.ConnectAsync(ipAddress, port).Wait(TimeSpan.FromSeconds(5));
                Console.WriteLine("****************************1WTF*****************");

                if(!isConnected)
                {
                    Debug.WriteLine("[PollSslClient] This tcpClient is disconnected.");
                }
                else
                {
                    Debug.WriteLine("[PollSslClient] This tcpClient is connected.");
                }
                Console.WriteLine("****************************0WTF*****************");

                await Task.Delay(1000);
            }
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

        public async Task ConsumeQueue()
        {
            if (consumingQueue) return;

            consumingQueue = true;

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
            await ConsumeQueue();
        }

        void EnqueueMessage(string requestId, string request)
        {
            if (queue.Any(o => o.Equals(requestId))) return;

            results[requestId] = null;
            queue.Enqueue(request);
        }

        /// <summary>
        /// Consume messages from the stream
        /// </summary>
        async Task ConsumeMessages()
        {
            if (readingStream) return;

            readingStream = true;

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
                Debug.WriteLine($"[ConsumeMessages] Error: {e.Message}, Stracktrace: {e.StackTrace}");

                readingStream = false;

                await ConsumeMessages();
            }
        }

#pragma warning disable IDE0051 // Remove unused private members
        async Task WaitForEmptyResult(string requestId)
#pragma warning restore IDE0051 // Remove unused private members
        {
            var loopDelay = 10;
            results.TryGetValue(requestId, out string val);
            while (!string.IsNullOrEmpty(val))
            {
                await Task.Delay(loopDelay);
            }
        }
    }
}
