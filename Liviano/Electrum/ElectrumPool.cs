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
using System.Reflection;
using System.Linq;
using System.IO;

using NBitcoin;
using Newtonsoft.Json;

using Liviano.Models;
using Liviano.Interfaces;
using Liviano.Extensions;
using Liviano.Exceptions;
using Liviano.Events;

using static Liviano.Electrum.ElectrumClient;

namespace Liviano.Electrum
{
    public class ElectrumPool : IElectrumPool
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

                    OnConnected?.Invoke(this, value);
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
        public event EventHandler<Server> OnConnected;
        public event EventHandler<Server> OnDisconnectedEvent;
        public event EventHandler OnDoneFindingPeersEvent;
        public event EventHandler OnCancelFindingPeersEvent;
        public event EventHandler<TxEventArgs> OnNewTransaction;
        public event EventHandler<TxEventArgs> OnUpdateTransaction;
        public event EventHandler OnSyncStarted;
        public event EventHandler OnSyncFinished;
        public event EventHandler OnWatchStarted;
        public event EventHandler<WatchAddressEventArgs> OnWatchAddressNotified;

        public Server[] AllServers { get; set; }

        public List<Server> ConnectedServers { get; set; }

        public ElectrumClient ElectrumClient { get; set; }

        public ElectrumPool(Server[] servers, Network network = null)
        {
            Network ??= network ?? Network.Main;
            AllServers ??= servers.Shuffle();
            ConnectedServers ??= new List<Server> { };
        }

        public async Task Connect(CancellationTokenSource cts = null)
        {
            await FindConnectedServers(cts);

            if (ConnectedServers.Count < MIN_NUMBER_OF_CONNECTED_SERVERS)
                await Connect(cts);
            else
                Save();
        }

        public async Task FindConnectedServers(CancellationTokenSource cts = null)
        {
            cts ??= new CancellationTokenSource();
            var cancellationToken = cts.Token;

            await Task.Factory.StartNew(() =>
            {
                foreach (var s in AllServers)
                {
                    Task.Factory.StartNew(async (o) =>
                    {
                        s.CancellationToken = cancellationToken;
                        s.OnConnectedEvent += HandleConnectedServers;

                        await s.ConnectAsync();
                    }, cancellationToken, TaskCreationOptions.AttachedToParent);
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            if (cancellationToken.IsCancellationRequested)
                OnCancelFindingPeersEvent?.Invoke(this, null);
            else
                OnDoneFindingPeersEvent?.Invoke(this, null);
        }

        public void RemoveServer(Server server)
        {
            lock (@lock) ConnectedServers.RemoveServer(server);
        }

        public void SetNewConnectedServer()
        {
            var oldCurrentServer = CurrentServer;
            var serversWithoutCurrent = GetNewConnectedServers().Shuffle();

            // We can't change server if it's the only one we have
            if (serversWithoutCurrent.Count() == 0)
            {
                Debug.WriteLine("[SetNewConnectedServer] Cannot get new list of servers without current one since it's the only server we have");

                return;
            }

            CurrentServer = serversWithoutCurrent.First();

            RemoveServer(oldCurrentServer);
        }

        public List<Server> GetNewConnectedServers()
        {
            return GetConnectedWithout(CurrentServer);
        }

        public List<Server> GetConnectedWithout(Server server)
        {
            return ConnectedServers.Where(s => s.Domain != server.Domain).ToList();
        }

        /// <summary>
        /// Saves the recently connected servers to disk or resource?
        /// </summary>
        public void Save()
        {
            var data = JsonConvert.SerializeObject(ConnectedServers, formatting: Formatting.Indented);

            if (string.IsNullOrEmpty(data)) return;

            File.WriteAllText(GetRecentServersFileName(Network), data);
        }

        /// <summary>
        /// Watches a wallet for new transactions
        /// </summary>
        /// <param name="wallet">A <see cref="IWallet"/> to watch</param>
        /// <param name="ct">A <see cref="CancellationToken"/> to cancel</param>
        public async Task WatchWallet(IWallet wallet, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            OnWatchStarted?.Invoke(this, null);

            foreach (var acc in wallet.Accounts) await WatchAccount(acc, ct);
        }

        /// <summary>
        /// Watches an account for new transactions
        /// </summary>
        /// <param name="acc">An <see cref="IAccount"/> to watch</param>
        /// <param name="ct">a <see cref="CancellationToken"/> to stop this</param>
        public async Task WatchAccount(IAccount acc, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            var changeAddresses = acc.GetChangeAddressesToWatch();
            var receiveAddresses = acc.GetReceiveAddressesToWatch();

            var addresses = new List<BitcoinAddress> {};

            addresses.AddRange(changeAddresses);
            addresses.AddRange(receiveAddresses);

            foreach (var addr in addresses)
                await Task.Factory.StartNew(
                    async o => await WatchAddress(acc, addr, receiveAddresses, changeAddresses, ct),
                    TaskCreationOptions.AttachedToParent,
                    ct
                );
        }

        /// <summary>
        /// Watches an address
        /// </summary>
        public async Task WatchAddress(
                IAccount acc,
                BitcoinAddress addr,
                BitcoinAddress[] receiveAddresses,
                BitcoinAddress[] changeAddresses,
                CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;
            var scriptHashStr = addr.ToScriptHash().ToHex();

            Debug.WriteLine($"[WatchAddress] Address: {addr} ScriptHash: {scriptHashStr}");

            await ElectrumClient.BlockchainScriptHashSubscribe(scriptHashStr, async (str) =>
            {
                Debug.WriteLine($"[WatchAddress][foundTxCallback] Started!");
                Debug.WriteLine($"[WatchAddress][foundTxCallback] Got status from BlockchainScriptHashSubscribe, hash: {scriptHashStr} status: {str}.");
                string status;
                try
                {
                    var oStatus = Deserialize<BlockchainScriptHashSubscribeNotification>(str);

                    status = string.Join(", ", oStatus.Params);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[WatchAddress] Cannot parse as a full result: {e.Message}... Trying with string result now");
                    var sStatus = Deserialize<ResultAsString>(str);

                    if (string.IsNullOrEmpty(sStatus.Result))
                    {
                        Debug.WriteLine($"[WatchAddress] Result is null");

                        return;
                    }
                    else
                        Debug.WriteLine($"[WatchAddress] Result is not null");

                    status = sStatus.Result;
                }

                OnWatchAddressNotified?.Invoke(
                    this,
                    new WatchAddressEventArgs(status, acc, addr)
                );

                var unspent = await ElectrumClient.BlockchainScriptHashListUnspent(scriptHashStr);

                foreach (var unspentResult in unspent.Result)
                {
                    var txHash = unspentResult.TxHash;
                    var height = unspentResult.Height;

                    var currentTx = acc.Txs.FirstOrDefault((i) => i.Id.ToString() == txHash);

                    var blkChainTxGet = await ElectrumClient.BlockchainTransactionGet(txHash);

                    var txHex = blkChainTxGet.Result;

                    // Tx is new
                    if (currentTx is null)
                    {
                        var tx = Tx.CreateFromHex(
                            txHex, acc, Network, receiveAddresses, changeAddresses,
                            GetOutValueFromTxInputs
                        );

                        acc.AddTx(tx);
                        OnNewTransaction?.Invoke(this, new TxEventArgs(tx, acc, addr));

                        return;
                    }

                    // A potential update if tx heights are different
                    if (currentTx.BlockHeight != height)
                    {
                        var tx = Tx.CreateFromHex(
                            txHex, acc, Network, receiveAddresses, changeAddresses,
                            GetOutValueFromTxInputs
                        );

                        acc.UpdateTx(tx);

                        OnUpdateTransaction?.Invoke(this, new TxEventArgs(tx, acc, addr));

                        // Here for safety, at any time somebody can add code to this
                        return;
                    }
                }
            });

            Debug.WriteLine("[WatchAddress] Disconnected. Connect again in 30 seconds");

            // calling watch again after a 30 second timeout because it should never finish
            await Task.Delay(30_000); // Wait a 30 seconds

            await WatchAddress(acc, addr, receiveAddresses, changeAddresses, ct);
        }

        /// <summary>
        /// Sync wallet
        /// </summary>
        /// <param name="wallet">a <see cref="IWallet"/> to sync</param>
        /// <param name="cancellationToken">a <see cref="CancellationToken"/> to stop this</param>
        public async Task SyncWallet(IWallet wallet, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            OnSyncStarted?.Invoke(this, null);

            foreach (var acc in wallet.Accounts)
            {
                await SyncAccount(acc, ct);
            }

            OnSyncFinished?.Invoke(this, null);
        }

        /// <summary>
        /// Syncs an account of the wallet
        /// </summary>
        /// <param name="acc">a <see cref="IAccount"/> to sync/param>
        /// <param name="ct">a <see cref="CancellationToken"/> to stop this</param>
        /// <param name="syncExternal">a <see cref="bool"/> to indicate to sync external addresses</param>
        /// <param name="syncInternal">a <see cref="bool"/> to indicate to sync internal addresses</param>
        public async Task SyncAccount(IAccount acc, CancellationToken ct, bool syncExternal = true, bool syncInternal = true)
        {
            var receiveAddressesIndex = acc.GetExternalLastIndex();
            var changeAddressesIndex = acc.GetInternalLastIndex();

            var receiveAddresses = acc.GetReceiveAddress(acc.GapLimit);
            var changeAddresses = acc.GetChangeAddress(acc.GapLimit);

            if (syncExternal)
            {
                foreach (var addr in receiveAddresses)
                {
                    if (ct.IsCancellationRequested) return;

                    await SyncAddress(acc, addr, receiveAddresses, changeAddresses, ct);
                    //await Task.Factory.StartNew(async o =>
                    //{
                        //await SyncAddress(acc, addr, receiveAddresses, changeAddresses, ct);
                    //}, TaskCreationOptions.AttachedToParent, ct);
                }

                acc.ExternalAddressesIndex = acc.GetExternalLastIndex();
            }

            if (syncInternal)
            {
                foreach (var addr in changeAddresses)
                {
                    if (ct.IsCancellationRequested) return;

                    //await Task.Factory.StartNew(async o =>
                    //{
                        await SyncAddress(acc, addr, receiveAddresses, changeAddresses, ct);
                    //}, TaskCreationOptions.AttachedToParent, ct);
                }

                acc.InternalAddressesIndex = acc.GetInternalLastIndex();
            }

            // Call SyncAccount with a new [internal/external]AddressesCount + GapLimit
            if ((acc.GetExternalLastIndex() > receiveAddressesIndex) && (acc.GetInternalLastIndex() > changeAddressesIndex))
            {
                // This is the default but we wanna be explicit
                await SyncAccount(acc, ct, syncInternal: true, syncExternal: true);
            }
            else if (acc.GetExternalLastIndex() > receiveAddressesIndex)
            {
                await SyncAccount(acc, ct, syncInternal: false, syncExternal: true);
            }
            else if (acc.GetInternalLastIndex() > changeAddressesIndex)
            {
                await SyncAccount(acc, ct, syncInternal: true, syncExternal: false);
            }
        }

        /// <summary>
        /// Syncs an address as a children task from the main SyncWallet
        /// </summary>
        /// <param name="acc">The <see cref="IAccount"/> that address comes from</param>
        /// <param name="addr">The <see cref="BitcoinAddress"/> to sync</param>
        /// <param name="receiveAddresses">A list of <see cref="BitcoinAddress"/> of type receive</param>
        /// <param name="changeAddresses">A list of <see cref="BitcoinAddress"/> of type change</param>
        /// <param name="ct">A <see cref="CancellationToken"/></param>
        public async Task SyncAddress(
                IAccount acc,
                BitcoinAddress addr,
                BitcoinAddress[] receiveAddresses,
                BitcoinAddress[] changeAddresses,
                CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            var isReceive = receiveAddresses.Contains(addr);
            var scriptHashStr = addr.ToScriptHash().ToHex();
            var addrLabel = isReceive ? "External" : "Internal";

            Debug.WriteLine(
                $"[GetAddressHistoryTask] Address: {addr} ({addrLabel}) scriptHash: {scriptHashStr}"
            );

            // Get history
            try
            {
                await ElectrumClient.BlockchainScriptHashGetHistory(scriptHashStr).ContinueWith(async result =>
                {
                    await InsertTransactionsFromHistory(
                        acc,
                        addr,
                        receiveAddresses,
                        changeAddresses,
                        result.Result,
                        ct
                    );
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[GetAddressHistoryTask] Error: {e}");

                SetNewConnectedServer();

                await SyncAddress(
                    acc,
                    addr,
                    receiveAddresses,
                    changeAddresses,
                    ct
                );

                return;
            }
        }

        public async Task<bool> Broadcast(Transaction transaction)
        {
            var txHex = transaction.ToHex();

            try
            {
                var broadcast = await ElectrumClient.BlockchainTransactionBroadcast(txHex);

                if (broadcast.Result != transaction.GetHash().ToString())
                {
                    Debug.WriteLine("[Broadcast] Error could not broadcast");

                    return false;
                }
            }
            catch (Exception err)
            {
                Debug.WriteLine($"[Broadcast] Error could not broadcast: {err.Message}");

                return false;
            }


            return true;
        }

        /// <summary>
        /// Insert transactions from a result of the electrum network
        /// </summary>
        /// <param name="acc">a <see cref="IAccount"/> address belong to</param>
        /// <param name="address">a <see cref="BitcoinAddress"/> that found this tx</param>
        /// <param name="receiveAddresses">a <see cref="BitcoinAddress[]"/> of the receive addresses (external)</param>
        /// <param name="changeAddresses">a <see cref="BitcoinAddress[]"/> of the change addresses (internal)</param>
        /// <param name="result">a <see cref="BlockchainScriptHashGetHistoryResult"/> to load txs from</param>
        async Task InsertTransactionsFromHistory(
                IAccount acc,
                BitcoinAddress addr,
                BitcoinAddress[] receiveAddresses,
                BitcoinAddress[] changeAddresses,
                BlockchainScriptHashGetHistoryResult
                result,
                CancellationToken ct)
        {
            foreach (var r in result.Result)
            {
                if (ct.IsCancellationRequested) return;

                Debug.WriteLine($"[Sync] Found tx with hash: {r.TxHash}");

                BlockchainTransactionGetResult txRes;
                try
                {
                    txRes = await ElectrumClient.BlockchainTransactionGet(r.TxHash);
                }
                catch (ElectrumException e)
                {
                    Console.WriteLine($"[Sync] Error: {e.Message}");

                    SetNewConnectedServer();

                    await InsertTransactionsFromHistory(acc, addr, receiveAddresses, changeAddresses, result, ct);
                    return;
                }

                var tx = Tx.CreateFromHex(
                    txRes.Result,
                    acc,
                    Network,
                    receiveAddresses,
                    changeAddresses,
                    GetOutValueFromTxInputs
                );

                var txAddresses = Transaction.Parse(
                    tx.Hex,
                    Network
                ).Outputs.Select(
                    (o) => o.ScriptPubKey.GetDestinationAddress(Network)
                );

                foreach (var txAddr in txAddresses)
                {
                    if (receiveAddresses.Contains(txAddr))
                    {
                        if (acc.UsedExternalAddresses.Contains(txAddr))
                            continue;

                        acc.UsedExternalAddresses.Add(txAddr);
                    }

                    if (changeAddresses.Contains(txAddr))
                    {
                        if (acc.UsedInternalAddresses.Contains(txAddr))
                            continue;

                        acc.UsedInternalAddresses.Add(txAddr);
                    }
                }

                if (acc.TxIds.Contains(tx.Id.ToString()))
                {
                    acc.UpdateTx(tx);

                    OnUpdateTransaction?.Invoke(this, new TxEventArgs(tx, acc, addr));
                }
                else
                {
                    acc.AddTx(tx);

                    OnNewTransaction?.Invoke(this, new TxEventArgs(tx, acc, addr));
                }
            }
        }

        /// <summary>
        /// This will get all the transactions out to the total to calculate fees
        /// </summary>
        /// <param name="inputs">A <see cref="TxInList"/> of the inputs from the tx</param>
        /// <returns>A <see cref="Money"/> with the outs value from N</returns>
        Money GetOutValueFromTxInputs(TxInList inputs)
        {
            Money total = 0L;

            foreach (var input in inputs)
            {
                var outIndex = input.PrevOut.N;
                var outHash = input.PrevOut.Hash.ToString();

                // Get the transaction from the input
                var task = ElectrumClient.BlockchainTransactionGet(outHash);
                task.Wait();

                var hex = task.Result.Result;
                var transaction = Transaction.Parse(hex, Network);
                var txOut = transaction.Outputs[outIndex];

                total += txOut.Value;
            }

            return total;
        }

        public static IElectrumPool Load(Network network = null)
        {
            network ??= Network.Main;

            ElectrumPool pool;

            Dictionary<string, Dictionary<string, string>> allServersData;

            List<Server> allServers;
            List<Server> recentServers;

            string allServersFileName;
            string recentServersFileName;
            string json;

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

            Task<Server[]> t = server.FindPeers();
            t.Wait();

            if (AllServers.ContainsAllServers(t.Result)) return;
            lock (@lock) if (ConnectedServers.ContainsAllServers(t.Result)) return;
            if (ConnectedServers.Count >= MAX_NUMBER_OF_CONNECTED_SERVERS) return;

            foreach (var s in t.Result)
            {
                if (AllServers.ContainsServer(s)) continue;
                lock (@lock) if (ConnectedServers.ContainsServer(s)) continue;

                s.ConnectAsync().Wait();

                lock (@lock) if (ConnectedServers.Count >= MAX_NUMBER_OF_CONNECTED_SERVERS) return;

                HandleConnectedServers(s, null);
            }
        }

        public async Task<BlockchainBlockHeadersInnerResult> DownloadHeaders(int fromHeight, int toHeight)
        {
            var res = await ElectrumClient.BlockchainBlockHeaders(fromHeight, toHeight);

            return res.Result;
        }

        public async Task<BlockchainHeadersSubscribeInnerResult> SubscribeToHeaders()
        {
            var res = await ElectrumClient.BlockchainHeadersSubscribe();

            return res.Result;
        }
    }
}
