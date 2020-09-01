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
using Liviano.Extensions;
using Liviano.Exceptions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Liviano.Electrum
{
    public class JsonRpcClient
    {
        int DEFAULT_NETWORK_TIMEOUT_INT = 5;
        TimeSpan DEFAULT_NETWORK_TIMEOUT = TimeSpan.FromSeconds(5.0);

        TimeSpan DEFAULT_TIMEOUT_FOR_SUBSEQUENT_DATA_AVAILABLE_SIGNAL_TO_HAPPEN = TimeSpan.FromMilliseconds(500.0);

        TimeSpan DEFAULT_TIME_TO_WAIT_BETWEEN_DATA_GAPS = TimeSpan.FromMilliseconds(1.0);

        readonly Server server;

        IPAddress ipAddress;

        int port;

        public string Host { get; private set; }

        public JsonRpcClient(Server server)
        {
            this.server = server;
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
                var maybeTimedOutIpAddress = await ResolveAsync(hostName).WithTimeout(DEFAULT_NETWORK_TIMEOUT);
                if (maybeTimedOutIpAddress == null) throw new TimeoutException($"Timed out connecting to {hostName}:{port}");
                return maybeTimedOutIpAddress;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
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

        bool TimesUp(NetworkStream _, List<byte> acc, DateTime initTime)
        {
            if (acc == null || !acc.Any())
            {
                if (DateTime.UtcNow > initTime + DEFAULT_NETWORK_TIMEOUT)
                {
                    throw new ElectrumException("No response received after request.");
                }

                return false;
            }

            return DateTime.UtcNow > initTime + DEFAULT_TIMEOUT_FOR_SUBSEQUENT_DATA_AVAILABLE_SIGNAL_TO_HAPPEN;
        }

        TcpClient Connect()
        {
            var tcpClient = new TcpClient(ipAddress.AddressFamily)
            {
                SendTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds),
                ReceiveTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds),
                ExclusiveAddressUse = true
            };

            var isConnected = tcpClient.ConnectAsync(ipAddress, port).Wait(DEFAULT_NETWORK_TIMEOUT);

            if (!isConnected)
            {
                throw new ElectrumException("Server is unresponsive.");
            }

            return tcpClient;
        }

        string Read(NetworkStream stream, List<byte> acc, DateTime initTime)
        {
            if (!stream.DataAvailable || !stream.CanRead)
            {
                if (TimesUp(stream, acc, initTime)) return WrapResult(acc);

                Thread.Sleep(DEFAULT_TIME_TO_WAIT_BETWEEN_DATA_GAPS);
                return Read(stream, acc, initTime);
            }

            var nullable = ReadByte(stream);
            if (nullable == null) return WrapResult(acc);

            byte curByte = nullable.Value;
            acc.Add(curByte);
            return Read(stream, acc, DateTime.UtcNow);
        }

        string RequestInternal(string request, bool useSsl = true)
        {
            Debug.WriteLine($"[RequestInternal] Sending request: {request}");

            if (!useSsl)
                return RequestInternalNonSsl(request);

            return RequestInternalSsl(request);
        }

        string RequestInternalSsl(string request)
        {
            using var tcpClient = Connect();
            using var stream = SslTcpClient.GetSslStream(tcpClient, Host);

            if (!stream.CanTimeout) return null; // Handle exception outside of Request()

            stream.ReadTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds);
            stream.WriteTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds);

            var bytes = Encoding.UTF8.GetBytes(request + "\n");

            stream.Write(bytes, 0, bytes.Length);

            stream.Flush();

            return SslTcpClient.ReadMessage(stream);
        }

        string RequestInternalNonSsl(string request)
        {
            using var tcpClient = Connect();
            using var stream = tcpClient.GetStream();

            if (!stream.CanTimeout) return null; // Handle exception outside of Request()

            stream.ReadTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds);
            stream.WriteTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds);

            var bytes = Encoding.UTF8.GetBytes(request + "\n");

            stream.Write(bytes, 0, bytes.Length);

            stream.Flush();

            return Read(stream, new List<byte>(), DateTime.UtcNow);
        }

        public async Task<string> Request(string request, bool useSsl = true)
        {
            //try
            //{
                Host = server.Domain;
                ipAddress = ResolveHost(server.Domain).Result;
                port = useSsl ? server.PrivatePort.Value : server.UnencryptedPort.Value;

                Debug.WriteLine(
                    $"[Request] Server: {Host}:{port} ({server.Version}) Request: {request}"
                );

                var result = await Task.Run(() => RequestInternal(request, useSsl)).WithTimeout(DEFAULT_NETWORK_TIMEOUT);

                if (result == null) throw new ElectrumException("Timeout when trying to communicate with server");

                return result;
            //}
            //catch (Exception ex)
            //{
                //Debug.WriteLine($"[Request] Could not process request: {request}");
                //Debug.WriteLine($"[Request] Failed for {server.Domain} at port {server.PrivatePort}: {ex.Message}");

                //throw new ElectrumException($"{ex.Message}");
            //}
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

            var stream = SslTcpClient.GetSslStream(tcpClient, Host);

            stream.ReadTimeout = Convert.ToInt32(TimeSpan.FromSeconds(DEFAULT_NETWORK_TIMEOUT_INT).TotalMilliseconds);
            stream.WriteTimeout = Convert.ToInt32(TimeSpan.FromSeconds(DEFAULT_NETWORK_TIMEOUT_INT).TotalMilliseconds);

            return stream;
        }

        public async Task Subscribe(string request, Action<string> callback, CancellationTokenSource cts = null)
        {
            Debug.WriteLine($"[Subscribe] {request}");

            if (cts is null) cts = new CancellationTokenSource();

            var requestBytes = Encoding.UTF8.GetBytes(request + "\n");
            var ct = cts.Token;

            using var stream = GetSslStream();

            // Subscribe requests don't have timeouts, we will get more responses from it
            stream.WriteTimeout = Timeout.Infinite;
            stream.ReadTimeout = Timeout.Infinite;

            stream.Write(requestBytes, 0, requestBytes.Length);
            stream.Flush();

            SslTcpClient.OnSubscriptionMessageEvent += (o, msg) =>
            {
                // Is this shit important?
                if (stream.GetHashCode() == o.GetHashCode())
                    callback(msg);
            };

            await Task.Factory.StartNew(
                (o) => SslTcpClient.ReadSubscriptionMessages(stream),
                TaskCreationOptions.LongRunning,
                ct
            );
        }
    }
}
