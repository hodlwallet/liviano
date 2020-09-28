//
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Liviano.Electrum;
using Liviano.Extensions;

namespace Liviano.Models
{
    public class Server
    {
        public const int VERSION_REQUEST_RETRY_DELAY = 1500;
        public const int VERSION_REQUEST_MAX_RETRIES = 3;

        [JsonProperty("ip")]
        public string Ip { get; set; }

        [JsonIgnore]
        public CancellationToken CancellationToken { get; set; }

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

        [JsonIgnore]
        public EventHandler OnConnectedEvent;

        [JsonIgnore]
        public EventHandler OnDisconnectedEvent;

        bool connected = false;

        [JsonIgnore]
        public bool Connected
        {
            get => connected;

            set
            {
                connected = value;

                if (Connected)
                    OnConnectedEvent?.Invoke(this, null);
                else
                    OnDisconnectedEvent?.Invoke(this, null);
            }
        }

        [JsonIgnore]
        public ElectrumClient ElectrumClient { get; set; }

        /// <summary>
        /// Downloads headers from this server, TODO store it somwhere
        /// </summary>
        public async Task DownloadHeaders()
        {
            await Task.Delay(10);
        }

        /// <summary>
        /// Connects by trying to get a version.
        /// </summary>
        /// <param name="retries">How many times we've retried</param>
        /// <returns></returns>
        public async Task ConnectAsync(int retries = 2)
        {
            Debug.WriteLine($"Connecting to {Domain}:{PrivatePort} at {DateTime.UtcNow}");

            if (CancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine("Cancellation requested, NOT CONNECTING!");

                return;
            }

            System.Version version;
            try
            {
                version = await ElectrumClient.ServerVersion();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error = {e.Message} {DateTime.UtcNow}");

                if (retries >= VERSION_REQUEST_MAX_RETRIES)
                    return;

                Connected = false;

                Debug.WriteLine($"Retry in {VERSION_REQUEST_RETRY_DELAY} ms");

                await Task.Delay(VERSION_REQUEST_RETRY_DELAY);
                await ConnectAsync(retries + 1);

                return;
            }

            if (version >= ElectrumClient.REQUESTED_VERSION)
            {
                Debug.WriteLine($"Connected {Domain}! at {DateTime.UtcNow}");

                Connected = true;
                return;
            }

            if (retries > VERSION_REQUEST_MAX_RETRIES)
            {
                Debug.WriteLine($"Failed to get version, retrying! at {DateTime.UtcNow}");

                Connected = false;

                Debug.WriteLine($"Retry in {VERSION_REQUEST_RETRY_DELAY} ms");

                await Task.Delay(VERSION_REQUEST_RETRY_DELAY);
                await ConnectAsync(retries + 1);

                return;
            }
        }

        public async Task<string> Banner()
        {
            var banner = await ElectrumClient.ServerBanner();

            return banner;
        }

        public async Task<bool> Ping()
        {
            var ping = await ElectrumClient.ServerDonationAddress();

            return ping == null;
        }

        public async Task<string> DonationAddress()
        {
            var donationAddress = await ElectrumClient.ServerDonationAddress();

            return donationAddress.Result;
        }

        public async Task<Server[]> FindPeers()
        {
            Debug.WriteLine($"[FindPeers] for {Domain}:{PrivatePort} at {DateTime.UtcNow}");

            try
            {
                var peers = await ElectrumClient.ServerPeersSubscribe();

                return ElectrumServers.FromPeersSubscribeResult(peers.Result).Servers.CompatibleServers().ToArray();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[FindPeers] Error: {e.Message}");

                // TODO ngl this shit should fail.

                return new Server[] { };
            }
        }

        public static Server FromPeersSubscribeResultItem(List<object> item)
        {
            // Example result:
            // [
            //   "107.150.45.210",
            //   "e.anonyhost.org",
            //   ["v1.0", "p10000", "t", "s995"]
            // ]
            // First IP and domain are the first 2
            var ip = (string)item[0];
            var domain = (string)item[1];

            var features = (JArray)item[2];

            // Parsing features
            var version = "";
            var pruning = "";
            var tcpPort = "";
            var sslPort = "";

            foreach (string f in features)
            {
                if (string.IsNullOrEmpty(f)) continue;

                switch ((char)f[0])
                {
                    case 'v':
                        version = f.Substring(1);
                        continue;
                    case 'p':
                        pruning = f.Substring(1);
                        continue;
                    case 't':
                        tcpPort = f.Substring(1);
                        continue;
                    case 's':
                        sslPort = f.Substring(1);
                        continue;
                    default:
                        continue;
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

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Domain) || PrivatePort is null)
                return base.ToString();

            return $"{Domain}:s{PrivatePort}";
        }

        public static Server FromString(string serverString)
        {
            var res = serverString.Split(':');

            if (res.Length != 2)
            {
                throw new ArgumentException($"Invalid server string: {serverString}");
            }

            var s = new Server
            {
                Domain = res[0],
                PrivatePort = int.Parse(res[1].Substring(1))
            };

            return s;
        }
    }

    public class ElectrumServers
    {
        public List<Server> Servers { get; set; }

        public ElectrumServers()
        {
            Servers = new List<Server> { };
        }

        public static ElectrumServers FromPeersSubscribeResult(List<List<object>> result)
        {

            var servers = new ElectrumServers();

            foreach (var item in result)
            {
                var server = Server.FromPeersSubscribeResultItem(item);

                servers.Servers.Add(server);
            }

            return servers;
        }

        public static ElectrumServers FromList(List<Server> servers)
        {
            return new ElectrumServers()
            {
                Servers = servers
            };
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
