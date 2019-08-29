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
using System.Collections.Generic;

using Newtonsoft.Json;

namespace Liviano.Models
{
    public class Server
    {
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
    }

    public class ElectrumServers
    {
        public List<Server> Servers { get; set; }

        public static ElectrumServers FromDictionary(Dictionary<string, Dictionary<string, string>> dict)
        {
            var servers = new ElectrumServers();
            servers.Servers = new List<Server>();

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