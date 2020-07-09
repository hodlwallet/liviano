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
using System.Text;
using System.Linq;
using System.Reflection;
using System.IO;

using NBitcoin;
using Newtonsoft.Json;

using Liviano.Models;
using Liviano.Interfaces;
using Liviano.Extensions;
using Liviano.Exceptions;
using static Liviano.Electrum.ElectrumClient;

namespace Liviano.Electrum
{
    public class ElectrumPool
    {
#if DEBUG
        public const int MIN_NUMBER_OF_CONNECTED_SERVERS = 1;
#else
        public const int MIN_NUMBER_OF_CONNECTED_SERVERS = 4;
#endif
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

        public event EventHandler<Tx> OnUpdateTransaction;

        public Server[] AllServers { get; set; }

        public List<Server> ConnectedServers { get; set; }

        public ElectrumClient ElectrumClient { get; private set; }

        public ElectrumPool(Server[] servers, Network network = null)
        {
            Network ??= network ?? Network.Main;
            AllServers ??= servers.Shuffle();
            ConnectedServers ??= new List<Server> { };
        }

        public async Task FindConnectedServersUntilMinNumber(CancellationTokenSource cts = null, Assembly assembly = null)
        {
            await FindConnectedServers(cts);

            if (ConnectedServers.Count < MIN_NUMBER_OF_CONNECTED_SERVERS)
                await FindConnectedServersUntilMinNumber(cts);
            else
                Save(assembly);
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

        public void SetNewConnectedServer()
        {
            var oldCurrentServer = CurrentServer;

            CurrentServer = GetNewConnectedServers().Shuffle().First();

            RemoveServer(oldCurrentServer);
        }

        public List<Server> GetNewConnectedServers()
        {
            return GetConnectedWithout(CurrentServer);
        }

        public List<Server> GetConnectedWithout(Server server)
        {
            return ConnectedServers.Where(s => s.Domain == server.Domain).ToList();
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

            await Task.Factory.StartNew(o =>
            {
                foreach (var acc in wallet.Accounts)
                {
                    var extCount = acc.ExternalAddressesCount;
                    var receiveAddresses = acc.GetReceiveAddress(acc.GapLimit + 1);
                    acc.ExternalAddressesCount = extCount;

                    var intCount = acc.InternalAddressesCount;
                    var changeAddresses = acc.GetChangeAddress(acc.GapLimit + 1);
                    acc.InternalAddressesCount = intCount;

                    foreach (var addr in receiveAddresses)
                        _ = GetAddressHistoryTask(acc, addr, receiveAddresses, changeAddresses, ct);

                    foreach (var addr in changeAddresses)
                        _ = GetAddressHistoryTask(acc, addr, receiveAddresses, changeAddresses, ct);
                }
            }, TaskCreationOptions.LongRunning, ct);
        }

        async Task GetAddressHistoryTask(IAccount acc, BitcoinAddress addr, BitcoinAddress[] receiveAddresses, BitcoinAddress[] changeAddresses, CancellationToken ct)
        {
            var isReceive = receiveAddresses.Contains(addr);

            await Task.Factory.StartNew(o =>
            {
                var scriptHashStr = addr.ToScriptHash().ToHex();

                var addrLabel = isReceive ? "External" : "Internal";
                Debug.WriteLine($"[GetAddressHistoryTask] Address: {addr} ({addrLabel}) scriptHash: {scriptHashStr}");

                // Get history
                try
                {
                    _ = ElectrumClient.BlockchainScriptHashGetHistory(scriptHashStr).ContinueWith(result =>
                    {
                        _ = InsertTransactionsFromHistory(acc, receiveAddresses, changeAddresses, result.Result, ct);
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{e}");

                    SetNewConnectedServer();

                    _ = GetAddressHistoryTask(acc, addr, receiveAddresses, changeAddresses, ct);

                    return;
                }
            }, TaskCreationOptions.AttachedToParent, ct);
        }

        /// <summary>
        /// Insert transactions from a result of the electrum network
        /// </summary>
        /// <param name="acc">a <see cref="IAccount"/> address belong to</param>
        /// <param name="receiveAddresses">a <see cref="BitcoinAddress[]"/> of the receive addresses (external)</param>
        /// <param name="changeAddresses">a <see cref="BitcoinAddress[]"/> of the change addresses (internal)</param>
        /// <param name="result">a <see cref="BlockchainScriptHashGetHistoryResult"/> to load txs from</param>
        async Task InsertTransactionsFromHistory(IAccount acc, BitcoinAddress[] receiveAddresses, BitcoinAddress[] changeAddresses, BlockchainScriptHashGetHistoryResult result, CancellationToken ct)
        {
            foreach (var r in result.Result)
            {
                if (ct.IsCancellationRequested) return;

#if DEBUG
                // Upps... This is what happens when you test some bitcoin wallets,
                // this happened because I sent to a change address so the software thinks is a receive...
                // now I don't have a way to tell if the tx is receive or send...
                if (r.TxHash == "45f4d79ea7754cfdb3be338d1e5d674d6f7f4dc5a1c71867b68b647bed788d00")
                    continue;
#endif
                Debug.WriteLine($"[Sync] Found tx with hash: {r.TxHash}");

                BlockchainTransactionGetVerboseResult txRes;
                try
                {
                    txRes = await ElectrumClient.BlockchainTransactionGetVerbose(r.TxHash);
                }
                catch (ElectrumException e)
                {
                    Console.WriteLine(e.Message);

                    SetNewConnectedServer();

                    await InsertTransactionsFromHistory(acc, receiveAddresses, changeAddresses, result, ct);
                    return;
                }

                var tx = Tx.CreateFromHex(
                    txRes.Hex,
                    txRes.Time,
                    acc,
                    Network,
                    r.Height,
                    receiveAddresses,
                    changeAddresses
                );

                var txAddresses = Transaction.Parse(
                    tx.Hex,
                    Network
                ).Outputs.Select(
                    (o) => o.ScriptPubKey.GetDestinationAddress(Network)
                );

                if (((Wallet)acc.Wallet).TxIds.Contains(tx.Id.ToString()))
                {
                    acc.UpdateTx(tx);

                    if (tx.AccountId == acc.Wallet.CurrentAccountId)
                        OnUpdateTransaction?.Invoke(this, tx);
                }
                else
                {
                    acc.AddTx(tx);

                    if (tx.AccountId == acc.Wallet.CurrentAccountId)
                        OnNewTransaction?.Invoke(this, tx);
                }

                foreach (var txAddr in txAddresses)
                {
                    if (receiveAddresses.Contains(txAddr))
                    {
                        if (acc.UsedExternalAddresses.Contains(txAddr))
                            continue;

                        acc.UsedExternalAddresses.Add(txAddr);
                        acc.SetAddressCount(txAddr, isReceive: true);
                    }

                    if (changeAddresses.Contains(txAddr))
                    {
                        if (acc.UsedInternalAddresses.Contains(txAddr))
                            continue;

                        acc.UsedInternalAddresses.Add(txAddr);
                        acc.SetAddressCount(txAddr, isReceive: false);
                    }
                }
            }
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

            Dictionary<string, Dictionary<string, string>> allServersData;

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
                allServersData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                allServers = ElectrumServers.FromDictionary(allServersData).Servers.CompatibleServers();

                if (recentServersStream.Length > 0)
                {
                    using var recentServersReader = new StreamReader(recentServersStream);
                    json = allServersReader.ReadToEnd();
                    recentServers = JsonConvert.DeserializeObject<List<Server>>(json);
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
                allServersData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
                allServers = ElectrumServers.FromDictionary(allServersData).Servers.CompatibleServers();

                recentServersFileName = GetRecentServersFileName(network);

                if (File.Exists(recentServersFileName))
                {
                    json = File.ReadAllText(recentServersFileName);
                    recentServers = JsonConvert.DeserializeObject<List<Server>>(json);
                }
                else
                {
                    recentServers = new List<Server> { };
                }
            }

            pool = new ElectrumPool(allServers.ToArray().Shuffle(), network);

            if (recentServers.Count > 0)
            {
                var recentServersWithClients = recentServers.Select(s =>
                {
                    s.ElectrumClient = new ElectrumClient(new JsonRpcClient(s));

                    return s;
                }).ToList();

                pool.ConnectedServers = recentServersWithClients;
                pool.CurrentServer = recentServersWithClients[0];
            }

            return pool;
        }

        static string GetRecentServersFileName(Network network)
        {
            return GetLocalConfigFilePath("Electrum", "servers", $"recent_servers_{network.Name.ToLower()}.json");
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

        public void HandleConnectedServers(object sender, EventArgs e)
        {
            var server = (Server)sender;

            if (server.CancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine("Cancellation requested, NOT HANDLING!");

                return;
            }

            lock (@lock)
            {
                if (ConnectedServers.ContainsServer(server))
                {
                    Debug.WriteLine($"Already connected to {server.Domain}:{server.PrivatePort}");

                    return;
                }

                ConnectedServers.Insert(0, server);

                Save();

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

                    Task.Factory.StartNew(() =>
                    {
                        s.ConnectAsync().Wait();

                        lock (@lock) if (ConnectedServers.Count >= MAX_NUMBER_OF_CONNECTED_SERVERS) return;

                        HandleConnectedServers(s, null);
                    }, TaskCreationOptions.AttachedToParent);
                }
            }, TaskCreationOptions.AttachedToParent).Wait();
        }
    }
}
