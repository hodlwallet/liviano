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
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Liviano.Models
{
    public class Server
    {
        public const int VERSION_REQUEST_RETRY_DELAY = 1500;
        public const int VERSION_REQUEST_MAX_RETRIES = 3;
        public const int PING_DELAY = 450_000;

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
        public EventHandler OnConnected;

        [JsonIgnore]
        public EventHandler OnDisconnected;

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
                        version = f[1..];
                        continue;
                    case 'p':
                        pruning = f[1..];
                        continue;
                    case 't':
                        tcpPort = f[1..];
                        continue;
                    case 's':
                        sslPort = f[1..];
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
                PrivatePort = int.Parse(res[1][1..])
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

                servers.Servers.Add(server);
            }

            return servers;
        }
    }
}
