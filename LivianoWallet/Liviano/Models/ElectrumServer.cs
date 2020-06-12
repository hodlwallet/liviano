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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Liviano.Electrum;
using Newtonsoft.Json;

namespace Liviano.Models
{
    public class Server
    {
        public const int RETRY_DELAY = 30000;

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
        /// <param name="retrying"><see langword="true"/> when retrying</param>
        /// <returns></returns>
        public async Task ConnectAsync(bool retrying = false)
        {
            Debug.WriteLine($"Got in! at {DateTime.UtcNow}");

            System.Version version = null;

            try
            {
                version = await ElectrumClient.ServerVersion(ElectrumClient.CLIENT_NAME, ElectrumClient.REQUESTED_VERSION);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error = {e.Message} {DateTime.UtcNow}");

                if (retrying)
                    return;

                Connected = false;
                //await Task.Delay(RETRY_DELAY);
                await ConnectAsync(retrying: true);
            }

            if (!(version is null) && version == ElectrumClient.REQUESTED_VERSION)
            {
                Debug.WriteLine($"Connected! at {DateTime.UtcNow}");

                Connected = true;
                return;
            }

            if (!retrying)
            {
                Debug.WriteLine($"Failed to get version, retrying! at {DateTime.UtcNow}");

                Connected = false;
                //await Task.Delay(RETRY_DELAY);
                await ConnectAsync(retrying: true);
            }
        }
    }

    public class ElectrumServers
    {
        public List<Server> Servers { get; set; }

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