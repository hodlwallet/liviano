//
// ElectrumPool.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2020 HODL Wallet
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

using Liviano.Models;
using Liviano.Extensions;
using System.Linq;
using System.Reflection;
using Liviano.Interfaces;
using System.IO;
using NBitcoin;
using Newtonsoft.Json;

namespace Liviano.Electrum
{
    public class ElectrumPool
    {
        public const int MIN_NUMBER_OF_CONNECTED_SERVERS = 2;
        public const int MAX_NUMBER_OF_CONNECTED_SERVERS = 20;
        readonly object @lock = new object();

        public bool Connected { get; private set; }

        public Network Network { get; set; }

        Server currentServer;
        public Server CurrentServer
        {
            get => currentServer;

            set
            {
                if (currentServer is null && !Connected)
                {
                    Connected = true;

                    OnConnectedEvent?.Invoke(this, value);
                }

                if (value is null && !(currentServer is null))
                {
                    Connected = false;

                    OnDisconnectedEvent?.Invoke(this, currentServer);
                }

                currentServer = value;
                ElectrumClient = currentServer.ElectrumClient;

                OnCurrentServerChangedEvent?.Invoke(this, CurrentServer);
            }
        }

        public event EventHandler<Server> OnCurrentServerChangedEvent;

        public event EventHandler<Server> OnConnectedEvent;

        public event EventHandler<Server> OnDisconnectedEvent;

        public event EventHandler OnDoneFindingPeersEvent;

        public Server[] AllServers { get; set; }

        public List<Server> ConnectedServers { get; set; }

        public ElectrumClient ElectrumClient { get; private set; }

        public ElectrumPool(Server[] servers, Network network = null)
        {
            Network ??= network ?? Network.Main;
            AllServers ??= servers.Shuffle();
            ConnectedServers ??= new List<Server> { };
        }

        public async Task FindConnectedServersUntilMinNumber()
        {
            await FindConnectedServers();

            if (ConnectedServers.Count < MIN_NUMBER_OF_CONNECTED_SERVERS)
                await FindConnectedServersUntilMinNumber();
        }

        public async Task FindConnectedServers()
        {
            await Task.Factory.StartNew(() =>
            {
                foreach (var s in AllServers)
                {
                    Task.Factory.StartNew(() =>
                    {
                        s.OnConnectedEvent += HandleConnectedServers;

                        // This makes it wait
                        var t1 = s.ConnectAsync();

                        t1.Wait();
                    }, TaskCreationOptions.AttachedToParent);
                }
            });

            OnDoneFindingPeersEvent?.Invoke(this, null);
        }

        public void RemoveServer(Server server)
        {
            lock (@lock) ConnectedServers.RemoveServer(server);
        }

        /// <summary>
        /// Saves the recently connected servers to disk or resource?
        /// </summary>
        public void Save(Assembly assembly = null)
        {
            if (assembly != null)
            {
                var resourceName = $"Resources.Electrum.servers.{Network.Name.ToLower()}.json";
                using var stream = assembly.GetManifestResourceStream(resourceName);

                // TODO Write to the stream

                return;
            }

            var recentServersFileName = GetRecentServersFileName(Network);
            var json = File.ReadAllText(recentServersFileName);
            var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            var recentServers = ElectrumServers.FromDictionary(data).Servers;
        }

        /// <summary>
        /// Loads the pool from the filesystem
        /// </summary>
        /// <param name="network">a <see cref="Network"/> to load files from</param>
        /// <returns>A new <see cref="ElectrumPool"/></returns>
        public static ElectrumPool Load(Network network = null)
        {
            network ??= Network.Main;

            var allServersFileName = GetAllServersFileName(network);
            var json = File.ReadAllText(allServersFileName);
            var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            var allServers = ElectrumServers.FromDictionary(data).Servers.CompatibleServers();

            var pool = new ElectrumPool(allServers.ToArray().Shuffle());

            var recentServersFileName = GetRecentServersFileName(network);
            json = File.ReadAllText(recentServersFileName);
            data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            var recentServers = ElectrumServers.FromDictionary(data).Servers.Shuffle();

            if (recentServers.Length > 0)
            {
                pool.ConnectedServers = recentServers.ToList();
                pool.CurrentServer = recentServers[0];
            }

            return pool;
        }

        static string GetRecentServersFileName(Network network)
        {
            return GetLocalConfigFilePath($"recent_servers_{network.Name.ToLower()}.json");
        }

        static string GetAllServersFileName(Network network)
        {
            return GetLocalConfigFilePath("Electrum", "servers", $"{network.Name.ToLower()}.json");
        }

        /// <summary>
        /// Gets a list of recently conneted servers, these would be ready to connect
        /// </summary>
        /// <returns>a <see cref="List{Server}"/> of the recent servers</returns>
        public static List<Server> GetRecentServers(Network network = null)
        {
            network ??= Network.Main;

            List<Server> recentServers = new List<Server>();
            var fileName = Path.GetFullPath(GetRecentServersFileName(network));

            if (!File.Exists(fileName))
                return recentServers;

            var content = File.ReadAllText(fileName);

            if (!string.IsNullOrEmpty(content))
                recentServers.AddRange(JsonConvert.DeserializeObject<Server[]>(content));

            return recentServers;
        }

        /// <summary>
        /// Overwrites recently connected servers, as intended for startup.
        /// </summary>
        public static void CleanRecentlyConnectedServersFile(Network network = null)
        {
            network ??= Network.Main;

            var fileName = Path.GetFullPath(GetRecentServersFileName(network));

            if (!File.Exists(fileName))
                return;

            File.WriteAllText(fileName, "");
        }

        public static string GetLocalConfigFilePath(params string[] fileNames)
        {
            return Path.Combine(
                Path.GetDirectoryName(
                    Assembly.GetCallingAssembly().Location
                ), string.Join(Path.DirectorySeparatorChar.ToString(), fileNames.ToArray())
            );
        }

        private void HandleConnectedServers(object sender, EventArgs e)
        {
            var server = (Server)sender;

            lock (@lock)
            {
                if (ConnectedServers.ContainsServer(server)) return;

                ConnectedServers.Insert(0, server);

                if (CurrentServer is null) CurrentServer = server;

                // If we have enough connected servers we stop looking for peers
                if (ConnectedServers.Count >= MAX_NUMBER_OF_CONNECTED_SERVERS) return;
            }

            Console.WriteLine($"Connected to: {server.Domain}:{server.PrivatePort}");

            Task<Server[]> t = server.FindPeersAsync();
            t.Wait();

            if (AllServers.ToList().ContainsAllServers(t.Result)) return;
            lock (@lock) if (ConnectedServers.ContainsAllServers(t.Result)) return;

            Task.Factory.StartNew(() =>
            {
                foreach (var s in t.Result)
                {
                    if (AllServers.ToList().ContainsServer(s)) continue;
                    lock (@lock) if (ConnectedServers.ContainsServer(s)) continue;

                    Task.Factory.StartNew(() =>
                    {
                        s.OnConnectedEvent += HandleConnectedServers;

                        s.ConnectAsync().Wait();
                    }, TaskCreationOptions.AttachedToParent);
                }
            }).Wait();
        }
    }
}
