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
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Liviano.Models;
using System.Xml.Schema;
using System.ComponentModel;
using System.Security.Cryptography;

namespace Liviano.Electrum
{
    public class JsonRpcTcpClient
    {
        TimeSpan DEFAULT_NETWORK_TIMEOUT = TimeSpan.FromSeconds(30.0);

        TimeSpan DEFAULT_TIMEOUT_FOR_SUBSEQUENT_DATA_AVAILABLE_SIGNAL_TO_HAPPEN = TimeSpan.FromMilliseconds(500.0);

        TimeSpan DEFAULT_TIME_TO_WAIT_BETWEEN_DATA_GAPS = TimeSpan.FromMilliseconds(1.0);

        readonly List<Server> _Servers;

        IPAddress _ipAddress;

        int _Port;

        public string Host { get; private set; }

        public JsonRpcTcpClient(List<Server> servers)
        {
            _Servers = servers;
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
                if (maybeTimedOutIpAddress == null) throw new TimeoutException(string.Format("Timed out connecting to {0}:{1}", hostName, _Port));
                return maybeTimedOutIpAddress;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new HttpListenerException(1, string.Format("DNS host entry lookup resulted in no records for {0}\n{1}", hostName, ex.Message));
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

        bool TimesUp(NetworkStream stream, List<byte> acc, DateTime initTime)
        {
            if (acc == null || !acc.Any())
            {
                if (DateTime.UtcNow > initTime + DEFAULT_NETWORK_TIMEOUT)
                {
                    throw new HttpListenerException(1, "No response received after request.");
                }

                return false;
            }

            return DateTime.UtcNow > initTime + DEFAULT_TIMEOUT_FOR_SUBSEQUENT_DATA_AVAILABLE_SIGNAL_TO_HAPPEN;
        }

        TcpClient Connect()
        {
            var tcpClient = new TcpClient(_ipAddress.AddressFamily);
            tcpClient.SendTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds);
            tcpClient.ReceiveTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds);

            var isConnected = tcpClient.ConnectAsync(_ipAddress, _Port).Wait(DEFAULT_NETWORK_TIMEOUT);

            if (!isConnected)
            {
                throw new HttpListenerException(1, "Server is unresponsive.");
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

        async Task<string> RequestInternal(string request)
        {
            using (var tcpClient = Connect())
            {
                var stream = tcpClient.GetStream();

                if (!stream.CanTimeout) return null; // Handle exception outside of Request()

                stream.ReadTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds);
                stream.WriteTimeout = Convert.ToInt32(DEFAULT_NETWORK_TIMEOUT.TotalMilliseconds);

                var bytes = Encoding.UTF8.GetBytes(request + "\n");

                stream.Write(bytes, 0, bytes.Length);

                stream.Flush();

                return Read(stream, new List<byte>(), DateTime.UtcNow);
            }
        }

        public async Task<string> Request(string request)
        {
            var rng = new Random();
            List<Server> popableServers = new List<Server>();
            popableServers.AddRange(_Servers);

            object _lock = new object();
            while (popableServers.Count > 0)
            {
                var index = rng.Next(popableServers.Count);
                var server = popableServers[index];

                Task.Run(async () =>
                {

                    try
                    {
                        Host = server.Domain;
                        _ipAddress = ResolveHost(server.Domain).Result;
                        _Port = server.PrivatePort.Value; // Make this dynamic.

                        var stringOption = await RequestInternal(request).WithTimeout(DEFAULT_NETWORK_TIMEOUT);
                        if (stringOption == null) throw new HttpListenerException(1, "Timeout when trying to communicate with UtxoCoin server");

                        return stringOption;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(string.Format("Request failed for {0} at port {1}: {2}\nAttempting to reconnect.", server.Domain, server.PrivatePort, ex.Message));
                    }

                    lock (_lock)
                    {
                        popableServers.RemoveAt(index);
                    }
                });

            }

            return null;
        }
    }
}
