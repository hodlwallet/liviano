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
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Liviano.Models;
using Liviano.Interfaces;
using Liviano.Extensions;
using System.Linq;
using System.Reflection;
using System.IO;
using NBitcoin;
using Newtonsoft.Json;
using System.Text;

using static Liviano.Electrum.ElectrumClient;

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

        public event EventHandler OnCancelFindingPeersEvent;

        public event EventHandler<Tx> OnNewTransaction;

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

        public async Task FindConnectedServers(CancellationTokenSource cts = null)
        {
            cts ??= new CancellationTokenSource();
            var cancellationToken = cts.Token;

            await Task.Factory.StartNew(() =>
            {
                foreach (var s in AllServers)
                {
                    Task.Factory.StartNew((o) =>
                    {
                        s.CancellationToken = cancellationToken;
                        s.OnConnectedEvent += HandleConnectedServers;

                        s.ConnectAsync().Wait();
                    }, cancellationToken, TaskCreationOptions.AttachedToParent);
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            if (cancellationToken.IsCancellationRequested)
            {
                OnCancelFindingPeersEvent?.Invoke(this, null);
            }
            else
            {
                OnDoneFindingPeersEvent?.Invoke(this, null);
            }
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
            var data = JsonConvert.SerializeObject(ConnectedServers);

            if (string.IsNullOrEmpty(data)) return;

            if (assembly != null)
            {
                var resourceName = $"Resources.Electrum.servers.recent_{Network.Name.ToLower()}.json";
                using var stream = assembly.GetManifestResourceStream(resourceName);

                var @bytes = Encoding.GetEncoding("UTF-8").GetBytes(data);
                stream.Write(@bytes, 0, @bytes.Length);

                return;
            }

            File.WriteAllText(GetRecentServersFileName(Network), data);
        }

        /// <summary>
        /// Sync wallet
        /// </summary>
        /// <param name="wallet">a <see cref="IWallet"/> to sync</param>
        public async Task SyncWallet(IWallet wallet, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return;

            var t = Task.Factory.StartNew(o => {
                foreach (var acc in wallet.Accounts)
                {
                    var receiveAddresses = acc.GetReceiveAddress(acc.GapLimit);
                    var changeAddresses = acc.GetChangeAddress(acc.GapLimit);

                    var addresses = new List<BitcoinAddress> { };

                    addresses.AddRange(receiveAddresses);
                    addresses.AddRange(changeAddresses);

                    foreach (var addr in receiveAddresses)
                    {
                        var t = Task.Factory.StartNew(o => {
                            var scriptHashStr = addr.ToScriptHash().ToHex();
                            var accAddrs = GetAccountAddresses(acc);

                            // This is like this on purpose, we should communicate with this via the event
                            // OnNewTransaction
                            _ = ElectrumClient.BlockchainScriptHashSubscribe(scriptHashStr, async (str) =>
                            {
                                var status = Deserialize<ResultAsString>(str);

                                if (string.IsNullOrEmpty(status.Result)) return;

                                try
                                {
                                    var unspent = await ElectrumClient.BlockchainScriptHashListUnspent(scriptHashStr);

                                    foreach (var unspentResult in unspent.Result)
                                    {
                                        var txHash = unspentResult.TxHash;
                                        var height = unspentResult.Height;

                                        var currentTx = acc.Txs.FirstOrDefault((i) => i.Id.ToString() == txHash);

                                        // Tx is new
                                        if (currentTx is null)
                                        {
                                            var blkChainTxGet = await ElectrumClient.BlockchainTransactionGet(txHash);
                                            var txHex = blkChainTxGet.Result;

                                            var tx = Tx.CreateFromHex(txHex, acc, Network, height, receiveAddresses, changeAddresses);
                                            acc.OnNewTransaction += (o, tx) => {
                                                OnNewTransaction?.Invoke(this, tx);
                                            };

                                            acc.AddTx(tx);

                                            return;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[Start] There was an error gathering UTXOs: {ex.Message}");
                                }
                            });
                        }, TaskCreationOptions.AttachedToParent, ct);
                    }

                    foreach (var addr in acc.GetChangeAddress(acc.GapLimit))
                    {
                        var t = Task.Factory.StartNew(o => {
                            var scriptHashStr = addr.ToScriptHash().ToHex();
                            var accAddrs = GetAccountAddresses(acc);
                            var t = ElectrumClient.BlockchainScriptHashSubscribe(scriptHashStr, (str) =>
                            {
                                var status = Deserialize<ResultAsString>(str);
                            });

                            t.Wait();
                        }, TaskCreationOptions.AttachedToParent, ct);
                    }
                }
            }, TaskCreationOptions.LongRunning, ct);

            await t;
        }

        Dictionary<string, BitcoinAddress[]> GetAccountAddresses(IAccount account, object @lock = null)
        {
            @lock = @lock ?? new object();
            var addresses = new Dictionary<string, BitcoinAddress[]>();

            if (account.AccountType == "paper")
            {
                // Paper accounts only have one address, that's the point
                addresses.Add("external", new BitcoinAddress[] { account.GetReceiveAddress() });
                addresses.Add("internal", new BitcoinAddress[] { });
            }
            else
            {
                // Everything else, very likely, is an HD Account.

                // We generate accounts until the gap limit is reached,
                // based on their respective external and internal addresses count
                // External addresses (receive)
                lock (@lock)
                {
                    var externalCount = account.ExternalAddressesCount;
                    account.ExternalAddressesCount = 0;
                    addresses.Add("external", account.GetReceiveAddress(externalCount + account.GapLimit));
                    account.ExternalAddressesCount = externalCount;

                    // Internal addresses (send)
                    var internalCount = account.InternalAddressesCount;
                    account.InternalAddressesCount = 0;
                    addresses.Add("internal", account.GetChangeAddress(internalCount + account.GapLimit));
                    account.InternalAddressesCount = internalCount;
                }
            }

            return addresses;
        }


        /// <summary>
        /// Loads the pool from the filesystem
        /// </summary>
        /// <param name="network">a <see cref="Network"/> to load files from</param>
        /// <returns>A new <see cref="ElectrumPool"/></returns>
        public static ElectrumPool Load(Network network = null, Assembly assembly = null)
        {
            network ??= Network.Main;

            ElectrumPool pool;

            Dictionary<string, Dictionary<string, string>> data;

            List<Server> allServers;
            List<Server> recentServers;

            string allServersFileName;
            string recentServersFileName;
            string json;

            if (assembly != null)
            {
                allServersFileName = $"Resources.Electrum.servers.{network.Name.ToLower()}.json";
                recentServersFileName = $"Resources.Electrum.servers.recent_{network.Name.ToLower()}.json";

                using var allServersStream = assembly.GetManifestResourceStream(allServersFileName);
                using var recentServersStream = assembly.GetManifestResourceStream(recentServersFileName);

                using var allServersReader = new StreamReader(allServersStream);
                json = allServersReader.ReadToEnd();
                data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                allServers = ElectrumServers.FromDictionary(data).Servers.CompatibleServers();

                if (recentServersStream.Length > 0)
                {
                    using var recentServersReader = new StreamReader(recentServersStream);
                    json = allServersReader.ReadToEnd();
                    data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                    recentServers = ElectrumServers.FromDictionary(data).Servers.CompatibleServers();
                }
                else
                {
                    recentServers = new List<Server> { };
                }
            }
            else
            {
                allServersFileName = GetAllServersFileName(network);
                json = File.ReadAllText(allServersFileName);
                data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                allServers = ElectrumServers.FromDictionary(data).Servers.CompatibleServers();

                recentServersFileName = GetRecentServersFileName(network);

                if (File.Exists(recentServersFileName))
                {
                    json = File.ReadAllText(recentServersFileName);
                    data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                    recentServers = ElectrumServers.FromDictionary(data).Servers.Shuffle().ToList();
                }
                else
                {
                    recentServers = new List<Server> { };
                }
            }

            pool = new ElectrumPool(allServers.ToArray().Shuffle());

            if (recentServers.Count > 0)
            {
                pool.ConnectedServers = recentServers;
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

            if (server.CancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine("Cancellation requested, NOT HANDLING!");

                return;
            }

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

            if (AllServers.ContainsAllServers(t.Result)) return;
            lock (@lock) if (ConnectedServers.ContainsAllServers(t.Result)) return;
            if (ConnectedServers.Count >= MAX_NUMBER_OF_CONNECTED_SERVERS) return;

            Task.Factory.StartNew(() =>
            {
                foreach (var s in t.Result)
                {
                    if (AllServers.ContainsServer(s)) continue;
                    lock (@lock) if (ConnectedServers.ContainsServer(s)) continue;

                    Task.Factory.StartNew(() => {
                        s.ConnectAsync().Wait();

                        lock (@lock) if (ConnectedServers.Count >= MAX_NUMBER_OF_CONNECTED_SERVERS) return;

                        HandleConnectedServers(s, null);
                    }, TaskCreationOptions.AttachedToParent);
                }
            }, TaskCreationOptions.AttachedToParent).Wait();
        }
    }
}
