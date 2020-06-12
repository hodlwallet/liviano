﻿//
// ElectrumServer.cs
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
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Liviano.Electrum;

namespace Liviano.Models
{
    public class Server
    {
        public const int VERSION_REQUEST_RETRY_DELAY = 1500;
        public const int VERSION_REQUEST_MAX_RETRIES = 3;

        public string Ip { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("pruning")]
        public string Pruning { get; set; }

        [JsonProperty("s", NullValueHandling = NullValueHandling.Ignore)]
        public int? PrivatePort { get; set; }

        [JsonProperty("t")]
        public int? UnencryptedPort { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        public EventHandler OnConnectedEvent;
        public EventHandler OnDisconnectedEvent;
        bool connected = false;
        public bool Connected
        {
            get
            {
                return connected;
            }

            set
            {
                connected = value;

                if (Connected)
                    OnConnectedEvent?.Invoke(this, null);
                else
                    OnDisconnectedEvent?.Invoke(this, null);
            }
        }

        public ElectrumClient ElectrumClient { get; set; }

        /// <summary>
        /// Connects by trying to get a version.
        /// </summary>
        /// <param name="retries">How many times we've retried</param>
        /// <returns></returns>
        public async Task ConnectAsync(int retries = 0)
        {
            Console.WriteLine($"Connecting to {Domain}:{PrivatePort} at {DateTime.UtcNow}");

            System.Version version;
            try
            {
                version = await ElectrumClient.ServerVersion(ElectrumClient.CLIENT_NAME, ElectrumClient.REQUESTED_VERSION);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error = {e.Message} {DateTime.UtcNow}");

                if (retries >= VERSION_REQUEST_MAX_RETRIES)
                    return;

                Connected = false;

                Console.WriteLine($"Retry in {VERSION_REQUEST_RETRY_DELAY} ms");

                await Task.Delay(VERSION_REQUEST_RETRY_DELAY);
                await ConnectAsync(retries + 1);

                return;
            }

            if (version == ElectrumClient.REQUESTED_VERSION)
            {
                Console.WriteLine($"Connected {Domain}! at {DateTime.UtcNow}");

                Connected = true;
                return;
            }

            if (retries >= VERSION_REQUEST_MAX_RETRIES)
            {
                Console.WriteLine($"Failed to get version, retrying! at {DateTime.UtcNow}");

                Connected = false;

                Console.WriteLine($"Retry in {VERSION_REQUEST_RETRY_DELAY} ms");

                await Task.Delay(VERSION_REQUEST_RETRY_DELAY);
                await ConnectAsync(retries + 1);
            }
        }

        public async Task<Server[]> FindPeers()
        {
            var servers = new Server[] { };

            Console.WriteLine($"Finding peers for {Domain}:{PrivatePort} at {DateTime.UtcNow}");

            var peers = await ElectrumClient.ServerPeersSubscribe();

            return servers;
        }

        public static Server FromPeersSubscribeItem(List<object> item)
        {
            // Example result:
            // [
            //   "107.150.45.210",
            //   "e.anonyhost.org",
            //   ["v1.0", "p10000", "t", "s995"]
            // ]
            // First IP and domain are the first 2
            var ip = (string) item[0];
            var domain = (string) item[1];

            // Parsing features
            var features = (string[]) item[2];

            var version = "";
            var pruning = "";
            var tcpPort = "";
            var sslPort = "";

            foreach (string f in features)
            {
                switch(f[0])
                {
                    case 'v':
                        version = f.Substring(1, f.Length);
                        break;
                    case 'p':
                        pruning = f.Substring(1, f.Length);
                        break;
                    case 't':
                        tcpPort = f.Substring(1, f.Length);
                        break;
                    case 's':
                        sslPort = f.Substring(1, f.Length);
                        break;
                    default:
                        break;
                }
            }

            var server = new Server
            {
                Ip = ip,
                Domain = domain,
                Pruning = pruning,
                Version = version
            };

            if (!string.IsNullOrEmpty(sslPort)) server.PrivatePort = int.Parse(sslPort);
            else server.PrivatePort = null;

            if (!string.IsNullOrEmpty(tcpPort)) server.UnencryptedPort = int.Parse(tcpPort);
            else server.UnencryptedPort = null;

            server.ElectrumClient = new ElectrumClient(
                new JsonRpcClient(server)
            );

            return server;
        }
    }

    public class ElectrumServers
    {
        public List<Server> Servers { get; set; }

        public static ElectrumServers FromPeersSubscribeResult(List<List<object>> result)
        {
            var servers = new ElectrumServers
            {
                Servers = new List<Server>()
            };

            foreach (var item in result)
            {
                var server = Server.FromPeersSubscribeItem(item);

                servers.Servers.Add(server);
            }

            return servers;
        }

        public static ElectrumServers FromDictionary(Dictionary<string, Dictionary<string, string>> dict)
        {
            var servers = new ElectrumServers
            {
                Servers = new List<Server>()
            };

            foreach (var item in dict)
            {
                var domainName = item.Key;
                var values = item.Value;

                var server = new Server
                {
                    Domain = domainName,
                    Pruning = values["pruning"],
                    Version = values["version"]
                };

                if (values.ContainsKey("s"))
                    server.PrivatePort = int.Parse(values["s"]);
                else
                    server.PrivatePort = null;

                if (values.ContainsKey("t"))
                    server.UnencryptedPort = int.Parse(values["t"]);
                else
                    server.UnencryptedPort = null;

                server.ElectrumClient = new ElectrumClient(
                    new JsonRpcClient(server)
                );

                servers.Servers.Add(server);
            }

            return servers;
        }
    }
}
