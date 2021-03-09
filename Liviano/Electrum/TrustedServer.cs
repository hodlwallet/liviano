//
// TrustedServer.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2021 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE
// OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
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
using Newtonsoft.Json.Linq;

namespace Liviano.Electrum
{
    public class TrustedServer : IElectrumPool
    {
        public const int RECONNECT_DELAY = 1000; // ms
        public const int HEADER_SIZE = 80; // bytes

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

        public ElectrumClient ElectrumClient { get; set; }
        public bool Connected { get; private set; }
        public Network Network { get; private set; }

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
        public event EventHandler<NewHeaderEventArgs> OnNewHeaderNotified;

        public TrustedServer(Server server, Network network)
        {
            CurrentServer = server;
            Network = network;
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

        public async Task Connect(CancellationTokenSource cts = null)
        {
            cts ??= new CancellationTokenSource();
            var cancellationToken = cts.Token;

            CurrentServer.CancellationToken = cancellationToken;
            CurrentServer.OnConnectedEvent += HandleConnectedServers;

            await CurrentServer.ConnectAsync();

            OnCurrentServerChangedEvent?.Invoke(this, CurrentServer);
            OnConnected?.Invoke(this, CurrentServer);

            // Periodic ping, every 450_000 ms
            await CurrentServer.PeriodicPing(pingFailedAtCallback: async (dt) =>
            {
                Console.WriteLine($"[Connect] Ping failed at {dt}. Reconnecting...");

                // TODO check if this is needed
                //CurrentServer.ElectrumClient = null;
                CurrentServer.OnConnectedEvent = null;

                await Task.Delay(RECONNECT_DELAY);
                await Connect(cts);
            }).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
                OnCancelFindingPeersEvent?.Invoke(this, null);
            else
                OnDoneFindingPeersEvent?.Invoke(this, null);
        }

        public void HandleConnectedServers(object sender, EventArgs e)
        {
            CurrentServer = (Server)sender;
        }

        public async Task WatchWallet(IWallet wallet, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            OnWatchStarted?.Invoke(this, null);

            foreach (var acc in wallet.Accounts) await WatchAccount(acc, ct);
        }

        public async Task SubscribeToHeaders(IWallet wallet, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            await ElectrumClient.BlockchainHeadersSubscribe(
                resultCallback: async (str) => {
                    Debug.WriteLine($"[SubscribeToHeaders][resultCallback] {str}");

                    if (ct.IsCancellationRequested) return;

                    var json = JObject.Parse(str);
                    var res = (JObject) json.GetValue("result");

                    var height = (int) res.GetValue("height");
                    var hex = (string) res.GetValue("hex");

                    wallet.LastBlockHeaderHex = hex;
                    wallet.Height = height;
                    wallet.LastBlockHeader = BlockHeader.Parse(hex, wallet.Network);

                    wallet.Storage.Save();

                    await DownloadHeaders(wallet, wallet.Height);

                    Debug.WriteLine($"[SubscribeToHeaders][resultCallback] Saved wallet");
                },
                notificationCallback: async (str) => {
                    var json = JObject.Parse(str);

                    var lastHeaderHex = (string) json.GetValue("hex");
                    var lastHeaderHeight = (long) json.GetValue("height");

                    if (ct.IsCancellationRequested) return;

                    if (string.Equals(lastHeaderHex, wallet.LastBlockHeaderHex)) return;

                    Debug.WriteLine($"[SubscribeToHeaders][notificationCallback] Got a new header hex: \n'{lastHeaderHex}'.");

                    // No need to download headers later
                    if (lastHeaderHeight == wallet.Height + 1)
                    {
                        wallet.Height = lastHeaderHeight;
                        wallet.LastBlockHeaderHex = lastHeaderHex;
                        wallet.LastBlockHeader = BlockHeader.Parse(lastHeaderHex, wallet.Network);

                        OnNewHeaderNotified?.Invoke(
                            this,
                            new NewHeaderEventArgs(
                                wallet, wallet.LastBlockHeaderHex, wallet.Height
                            )
                        );

                        Debug.WriteLine($"[SubscribeToHeaders][notificationCallback] Set new height '{wallet.Height}' header hex: \n'{wallet.LastBlockHeaderHex}'");

                        wallet.Storage.Save();

                        return;
                    }

                    await DownloadHeaders(wallet, wallet.Height);
                }
            );
        }

        public async Task DownloadHeaders(IWallet wallet, long fromHeight)
        {
            Debug.WriteLine($"[DownloadHeaders] From height: {fromHeight}");

            var res = (await ElectrumClient.BlockchainBlockHeaders(fromHeight + 1)).Result;

            var count = res.Count;
            var hex = res.Hex;
            var max = res.Max;

            for (int i = 0; i < count; i++)
            {
                var headerChars = hex.Skip(i * HEADER_SIZE * 2).Take(HEADER_SIZE * 2).ToArray();
                var headerHex = new string(headerChars);

                wallet.Height = ++wallet.Height;
                wallet.LastBlockHeaderHex = headerHex;
                wallet.LastBlockHeader = BlockHeader.Parse(headerHex, wallet.Network);

                Debug.WriteLine($"[DownloadHeaders] Set new height '{wallet.Height}' header hex: \n'{wallet.LastBlockHeaderHex}'");

                OnNewHeaderNotified?.Invoke(
                    this,
                    new NewHeaderEventArgs(
                        wallet, wallet.LastBlockHeaderHex, wallet.Height
                    )
                );
            }

            // TODO This is too trusty... we need to handle reorgs as well

            wallet.Storage.Save();

            Debug.WriteLine($"[DownloadHeaders] Saved wallet");
        }

        public static IElectrumPool Load(Network network = null)
        {
            network ??= Network.Main;

            var serverFilename = GetServerFilename(network);
            var json = File.ReadAllText(serverFilename);

            var jsonData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            var server = ElectrumServers.FromDictionary(jsonData).Servers.CompatibleServers()[0];

            return new TrustedServer(server, network);
        }

        IElectrumPool IElectrumPool.Load(Network network)
        {
            network ??= Network.Main;

            return Load(network);
        }

        static string GetServerFilename(Network network = null)
        {
            network ??= Network.Main;

            return GetLocalConfigFilePath("Electrum", "servers", $"hodlwallet_{network.Name.ToLower()}.json");
        }

        static string GetLocalConfigFilePath(params string[] fileNames)
        {
            return Path.Combine(
                Path.GetDirectoryName(
                    Assembly.GetCallingAssembly().Location
                ), string.Join(Path.DirectorySeparatorChar.ToString(), fileNames.ToArray())
            );
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

            var addresses = new List<BitcoinAddress> { };

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
        async Task WatchAddress(
                IAccount acc,
                BitcoinAddress addr,
                BitcoinAddress[] receiveAddresses,
                BitcoinAddress[] changeAddresses,
                CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            var scriptHashStr = addr.ToScriptHash().ToHex();
            var receiveOrSend = receiveAddresses.Contains(addr) ? "Receive" : "Send";

            Debug.WriteLine($"[WatchAddress] Address: {addr} ({receiveOrSend}) ScriptHash: {scriptHashStr}");

            await ElectrumClient.BlockchainScriptHashSubscribe(
                scriptHashStr,
                resultCallback: (str) => {
                    Debug.WriteLine($"[WatchAddress][resultCallback] Got status from BlockchainScriptHashSubscribe, hash: {scriptHashStr} status: {str}.");

                    Debug.WriteLine($"[WatchAddress][resultCallback] Status: '{str}'.");

                    // TODO Status data structure description: https://electrumx-spesmilo.readthedocs.io/en/latest/protocol-basics.html#status
                    // I'm not sure what to do with it... What matters are the notifications and those are described bellow
                },
                notificationCallback: async (str) => {
                    Debug.WriteLine($"[WatchAddress][notificationCallback] Notification: '{str}'.");

                    if (string.IsNullOrEmpty(str))
                    {
                        Debug.WriteLine($"[WatchAddress][notificationCallback] Status is null or empty");

                        return;
                    }

                    Debug.WriteLine($"[WatchAddress][notificationCallback] Status: '{str}'.");

                    OnWatchAddressNotified?.Invoke(
                        this,
                        new WatchAddressEventArgs(str, acc, addr)
                    );

                    // TODO the following code should not be implemented like this... but it is
                    // because I don't understand the status data structure, rather, I need to save
                    // the transactions with the height reported before...

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
                                txHex, height, acc, Network, receiveAddresses, changeAddresses,
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
                                txHex, height, acc, Network, receiveAddresses, changeAddresses,
                                GetOutValueFromTxInputs
                            );

                            acc.UpdateTx(tx);

                            OnUpdateTransaction?.Invoke(this, new TxEventArgs(tx, acc, addr));
                        }
                    }
                }
            );
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
                }

                acc.ExternalAddressesIndex = acc.GetExternalLastIndex();
            }

            if (syncInternal)
            {
                foreach (var addr in changeAddresses)
                {
                    if (ct.IsCancellationRequested) return;

                    await SyncAddress(acc, addr, receiveAddresses, changeAddresses, ct);
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
        async Task SyncAddress(
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
                BlockchainScriptHashGetHistoryResult result,
                CancellationToken ct)
        {
            foreach (var r in result.Result)
            {
                if (ct.IsCancellationRequested) return;

                Debug.WriteLine($"[Sync] Found tx with hash: {r.TxHash}, height: {r.Height}, fee: {r.Fee}");

                BlockchainTransactionGetResult txRes;
                try
                {
                    txRes = await ElectrumClient.BlockchainTransactionGet(r.TxHash);
                }
                catch (ElectrumException e)
                {
                    Console.WriteLine($"[Sync] Error: {e.Message}");

                    await InsertTransactionsFromHistory(acc, addr, receiveAddresses, changeAddresses, result, ct);
                    return;
                }

                var tx = Tx.CreateFromHex(
                    txRes.Result,
                    r.Height,
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
    }
}
