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

        bool readingStream = false;
        bool consumingQueue = false;
        ConcurrentDictionary<int, string> results;
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

            results = new ConcurrentDictionary<int, string>(initialCapacity, concurrencyLevel);
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
            var requestId = (int) json.GetValue("id");

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

        public async Task Subscribe(string request, Action<string> callback, CancellationTokenSource cts = null)
        {
            Host = server.Domain;
            ipAddress = ResolveHost(server.Domain).Result;
            port = server.PrivatePort.Value;

            tcpClient ??= Connect();
            sslStream ??= SslTcpClient.GetSslStream(tcpClient, Host);

            if (cts is null) cts = new CancellationTokenSource();

            var ct = cts.Token;
            var json = JObject.Parse(request);
            var requestId = (int) json.GetValue("id");

            // TODO should use this right?
            //var req = ElectrumClient.Deserialize<ElectrumClient.Request>(request);

            _ = ConsumeMessages();

            EnqueueMessage(requestId, request);

            _ = ConsumeQueue();

            await Task.Factory.StartNew(
                o => CallbackOnResult(requestId, callback),
                TaskCreationOptions.LongRunning,
                ct
            );
        }

        async Task<string> GetResult(int requestId)
        {
            var delay = 100;

            // Wait for new messages' responses
            while (results[requestId] == null) { await Task.Delay(delay); }

            // Now the result is ready
            var res = results[requestId];

            results.Remove(requestId, out _);

            return res;
        }

        async Task CallbackOnResult(int requestId, Action<string> callback)
        {
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

                        var json = (JObject) JsonConvert.DeserializeObject(req);
                        var requestId = (int) json.GetValue("id");
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

        void EnqueueMessage(int requestId, string request)
        {
            results[requestId] = null;
            queue.Enqueue(request);
        }

        async Task ConsumeMessages(SslStream newStream = null)
        {
            if (readingStream) return;

            readingStream = true;

            var stream = newStream == null ? sslStream : newStream;

            try
            {
                await SslTcpClient.ReadMessagesFrom(stream, (msgs) =>
                {
                    foreach (var msg in msgs.Split('\n'))
                    {
                        if (string.IsNullOrEmpty(msg)) continue;

                        var json = (JObject) JsonConvert.DeserializeObject(msg);
                        var requestId = (int) json.GetValue("id");

                        results[requestId] = msg;
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
    }
}
