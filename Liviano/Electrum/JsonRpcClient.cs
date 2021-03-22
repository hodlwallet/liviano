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
using System.Collections.Generic;
using System.Net.Security;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
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
        readonly int DEFAULT_NETWORK_TIMEOUT_INT = 30;
        TimeSpan DEFAULT_NETWORK_TIMEOUT = TimeSpan.FromSeconds(30.0);
        TimeSpan DEFAULT_TIMEOUT_DATA_AVAILABLE = TimeSpan.FromMilliseconds(500.0);
        TimeSpan DEFAULT_TIME_TO_WAIT_BETWEEN_DATA_GAPS = TimeSpan.FromMilliseconds(1.0);

        readonly Server server;

        IPAddress ipAddress;

        TcpClient tcpClient;
        SslStream sslStream;

        [ThreadStatic] bool readingStream = false;
        bool consumingQueue = false;
        ConcurrentDictionary<string, string> results;
        ConcurrentQueue<string> queue;

        int port;
        public string Host { get; private set; }

        object @lock = new object();

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

        async Task<IPAddress> ResolveAsync(string hostName)
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

        string WrapResult(List<byte> acc)
        {
            return Encoding.UTF8.GetString(acc.ToArray());
        }

        byte? ReadByte(NetworkStream stream)
        {
            int num = stream.ReadByte();

            if (num == -1) return null;

            return Convert.ToByte(num);
        }

        bool TimesUp(List<byte> acc, DateTime initTime)
        {
            if (acc == null || !acc.Any())
            {
                if (DateTime.UtcNow > initTime + DEFAULT_NETWORK_TIMEOUT)
                {
                    throw new ElectrumException("No response received after request.");
                }

                return false;
            }

            return DateTime.UtcNow > initTime + DEFAULT_TIMEOUT_DATA_AVAILABLE;
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

            // TODO Usecured tcp streams support back

            return await RequestInternalSsl(request);
        }

        async Task<string> RequestInternalSsl(string request)
        {
            tcpClient ??= Connect();
            sslStream ??= SslTcpClient.GetSslStream(tcpClient, Host);

            var json = JObject.Parse(request);
            var requestId = (string) json.GetValue("id");

            // TODO should use this right?
            //var req = ElectrumClient.Deserialize<ElectrumClient.Request>(request);

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

        public SslStream GetSslStream()
        {
            Host = server.Domain;
            ipAddress = ResolveHost(server.Domain).Result;
            port = server.PrivatePort.Value;

            Debug.WriteLine(
                $"[GetSslStream] From: {Host}:{port} ({server.Version})"
            );

            var tcpClient = Connect();

            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            var sslStream = SslTcpClient.GetSslStream(tcpClient, Host);

            sslStream.ReadTimeout = Convert.ToInt32(TimeSpan.FromSeconds(DEFAULT_NETWORK_TIMEOUT_INT).TotalMilliseconds);
            sslStream.WriteTimeout = Convert.ToInt32(TimeSpan.FromSeconds(DEFAULT_NETWORK_TIMEOUT_INT).TotalMilliseconds);

            return sslStream;
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
            var requestId = (string) json.GetValue("id");
            var method = (string) json.GetValue("method");

            _ = ConsumeMessages();

            EnqueueMessage(requestId, request);

            _ = ConsumeQueue();

            resultCallback(await GetResult(requestId));

            if (string.Equals(method, "blockchain.scripthash.subscribe"))
            {
                var @params = (JArray) json.GetValue("params");
                var scripthash = (string) @params[0];

                results[scripthash] = null;
                requestId = scripthash;
            }
            else if (string.Equals(method, "blockchain.headers.subscribe"))
            {
                results["blockchain.headers.subscribe"] = null;
                requestId = "blockchain.headers.subscribe";
            }

            await Task.Factory.StartNew(
                o => CallbackOnResult(requestId, notificationCallback),
                TaskCreationOptions.LongRunning,
                ct
            );
        }

        async Task<string> GetResult(string requestId)
        {
            var loopDelay = 100;

            // Wait for new messages' responses
            while (!results.ContainsKey(requestId) || results[requestId] == null)
                await Task.Delay(loopDelay);

            // Now the result is ready
            var res = results[requestId];

            results.TryRemove(requestId, out _);

            return res;
        }

        async Task CallbackOnResult(string requestId, Action<string> callback)
        {
            Debug.WriteLine($"[CallbackOnResult] Callback for requestId: {requestId}");

            callback(await GetResult(requestId));
        }

        public async Task ConsumeQueue()
        {
            if (consumingQueue) return;

            consumingQueue = true;

            var loopDelay = 10;

            try
            {
                while (true)
                {
                    while (!queue.IsEmpty)
                    {
                        queue.TryDequeue(out string req);

                        var json = JsonConvert.DeserializeObject<JObject>(req);
                        var requestId = (string) json.GetValue("id");
                        var bytes = Encoding.UTF8.GetBytes(req + "\n");

                        await sslStream.WriteAsync(bytes, 0, bytes.Length);
                        await sslStream.FlushAsync();
                    }

                    await Task.Delay(loopDelay);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[ConsumeQueue] Error: {e.StackTrace}");

                consumingQueue = false;

                throw;
            }
        }

        void EnqueueMessage(string requestId, string request)
        {
            results[requestId] = null;
            queue.Enqueue(request);
        }

        /// <summary>
        /// Consume messages from the stream
        /// </summary>
        /// <param name="newStream">Read from a new stream not the one we already have set</param>
        /// <param name="scripthash">Subscribe to a scripthash</param>
        async Task ConsumeMessages(SslStream newStream = null, string scripthash = null)
        {
            if (readingStream) return;

            readingStream = true;

            var stream = newStream == null ? sslStream : newStream;

            try
            {
                await SslTcpClient.ReadMessagesFrom(stream, async (msgs) =>
                {
                    foreach (var msg in msgs.Split('\n'))
                    {
                        if (string.IsNullOrEmpty(msg)) continue;

                        Debug.WriteLine($"[ConsumeMessages] '{msg}'.");

                        var json = JsonConvert.DeserializeObject<JObject>(msg);

                        if (json.ContainsKey("method")) // A subscription notification
                        {
                            Debug.WriteLine("[ConsumeMessages] A response result for a notification");

                            var @params = json.GetValue("params");
                            var method = (string) json.GetValue("method");

                            if (string.Equals(method, "blockchain.scripthash.subscribe"))
                            {
                                var scripthash = (string) @params[0];
                                var status = (string) @params[1];

                                Debug.WriteLine($"[ConsumeMessages] Scripthash ({scripthash}) new status: {(string) @params[1]}");

                                // See below's fixme
                                //await WaitForEmptyResult(scripthash);

                                results[scripthash] = status;
                            }
                            else if (string.Equals(method, "blockchain.headers.subscribe"))
                            {
                                var newHeader = (JObject) @params[0];
                                Debug.WriteLine($"[ConsumeMessages] New header: {newHeader}");

                                //await WaitForEmptyResult("blockchain.headers.subscribe");

                                results["blockchain.headers.subscribe"] = newHeader.ToString(Formatting.None);
                            }
                        }
                        else
                        {
                            var requestId = (string) json.GetValue("id");

                            Debug.WriteLine($"[ConsumeMessages] A response result for id: {requestId} (msg: {msg})");

                            // FIXME This is a bug, there should always be an empty result...
                            //await WaitForEmptyResult(requestId);

                            results[requestId] = msg;
                        }

                    }
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[ConsumeMessages] Error: {e.StackTrace}");

                readingStream = false;

                await ConsumeMessages();
            }
        }

        async Task WaitForEmptyResult(string requestId)
        {
            // Wait for results to be null
            var loopDelay = 100;

            while (HasResult(requestId)) await Task.Delay(loopDelay);
        }

        bool HasResult(string requestId)
        {
            lock (@lock)
            {
                var containsKey = results.ContainsKey(requestId);

                results.TryGetValue(requestId, out string val);

                return containsKey && !string.IsNullOrEmpty(val);
            }
        }
    }
}
