//
// Wallet.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using NBitcoin;

using Liviano.Interfaces;
using Liviano.Accounts;
using Liviano.Utilities;
using Liviano.Bips;
using Liviano.Storages;
using Liviano.Models;
using Liviano.Electrum;
using Liviano.Extensions;

using static Liviano.Electrum.ElectrumClient;

namespace Liviano
{
    public class Wallet : IWallet
    {
        const string DEFAULT_WALLET_NAME = "Bitcoin Wallet";
        const string DEFAULT_ACCOUNT_NAME = "Bitcoin Account";

        Key _PrivateKey;

        ExtKey _ExtKey;

        public string[] AccountTypes => new string[] { "bip141", "bip44", "bip49", "bip84", "paper", "wasabi" };

        public string Id { get; set; }

        public string Name { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }

        public Network Network { get; set; }

        public string EncryptedSeed { get; set; }

        public byte[] ChainCode { get; set; }

        public string CurrentAccountId { get; set; }

        IAccount _CurrentAccount;
        public IAccount CurrentAccount
        {
            get
            {
                if (_CurrentAccount is null || _CurrentAccount.Id != CurrentAccountId)
                {
                    _CurrentAccount = Accounts.FirstOrDefault((a) => a.Id == CurrentAccountId);
                }

                return _CurrentAccount;
            }

            set
            {
                CurrentAccountId = value.Id;
                _CurrentAccount = value;
            }
        }

        public List<string> TxIds { get; set; }
        public List<Tx> Txs { get; set; }

        public List<string> AccountIds { get; set; }
        public List<IAccount> Accounts { get; set; }

        IStorage _Storage;
        Assembly _Assembly;

        public event EventHandler SyncStarted;
        public event EventHandler SyncFinished;

        public event EventHandler<Tx> OnNewTransaction;
        public event EventHandler<Tx> OnUpdateTransaction;

        public IStorage Storage
        {
            get => _Storage;
            set
            {
                _Storage = value;

                _Storage.Id = Id;
                _Storage.Network = Network;
                _Storage.Wallet = this;
            }
        }

        public void Init(string mnemonic, string password = "", string name = null, Network network = null, DateTimeOffset? createdAt = null, IStorage storage = null, Assembly assembly = null)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            Id = Id ?? Guid.NewGuid().ToString();
            Name = Name ?? name ?? DEFAULT_WALLET_NAME;

            Network = Network ?? network ?? Network.Main;

            CreatedAt = CreatedAt ?? createdAt ?? DateTimeOffset.UtcNow;

            Storage = Storage ?? storage ?? new FileSystemStorage(Id, Network);

            TxIds = TxIds ?? new List<string>();
            Txs = Txs ?? new List<Tx>();

            AccountIds = AccountIds ?? new List<string>();
            Accounts = Accounts ?? new List<IAccount>();

            CurrentAccountId = CurrentAccountId ?? null;
            _CurrentAccount = _CurrentAccount ?? null;

            _Assembly = assembly;

            var mnemonicObj = Hd.MnemonicFromString(mnemonic);
            var extKey = Hd.GetExtendedKey(mnemonicObj, password);

            EncryptedSeed = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network).ToWif();
            ChainCode = extKey.ChainCode;

            // To cache them
            GetPrivateKey(password);
            GetExtendedKey(password);
        }

        /// <summary>
        /// Gets the private key, and puts it into <see cref="_PrivateKey"/>
        /// </summary>
        /// <param name="password"></param>
        /// <param name="forcePasswordVerification"></param>
        /// <returns></returns>
        public Key GetPrivateKey(string password = "", bool forcePasswordVerification = false)
        {
            if (_PrivateKey == null || forcePasswordVerification)
                _PrivateKey = Hd.DecryptSeed(EncryptedSeed, Network, password);

            return _PrivateKey;
        }

        /// <summary>
        /// Gets the ext key and puts it into <see cref="_ExtKey"/>
        /// </summary>
        /// <param name="password"></param>
        /// <param name="forcePasswordVerification"></param>
        /// <returns></returns>
        public ExtKey GetExtendedKey(string password = "", bool forcePasswordVerification = false)
        {
            Guard.NotNull(_PrivateKey, nameof(_PrivateKey));
            Guard.NotNull(ChainCode, nameof(ChainCode));

            if (forcePasswordVerification)
                _PrivateKey = GetPrivateKey(password, forcePasswordVerification);

            if (_ExtKey is null || forcePasswordVerification)
                _ExtKey = new ExtKey(_PrivateKey, ChainCode);

            return _ExtKey;
        }

        /// <summary>
        /// Adds an account to the wallet
        /// </summary>
        /// <param name="type">Check <see cref="Wallet.AccountTypes"/> for the list</param>
        /// <param name="name">Name of the account default is <see cref="Wallet.DEFAULT_WALLET_NAME"/></param>
        /// <param name="options">
        ///     Options can be passed to this  function this way, e.g:
        ///
        ///     AddAccount("bip141", "My Bitcoins", new {Wallet = this, WalletId = this.Id, Network = Network.TestNet})
        /// </param>
        public void AddAccount(string type = "", string name = null, object options = null)
        {
            var account = NewAccount(type, name, options);

            AccountIds.Add(account.Id);
            Accounts.Add(account);

            if (!string.IsNullOrEmpty(CurrentAccountId)) return;

            CurrentAccount = account;
            CurrentAccountId = account.Id;
        }

        public async Task Start()
        {
            Debug.WriteLine($"[Start] Starting wallet: {Id}");

            var electrum = await GetElectrumClient();
            var tasks = new List<Task>();
            var @lock = new object();

            foreach (var account in Accounts)
            {
                Debug.WriteLine($"Listening for account: {account.Name} ({account.AccountType} : {account.HdPath})");

                var externalCount = account.ExternalAddressesCount;
                var addresses = account.GetReceiveAddress(externalCount + account.GapLimit);
                account.ExternalAddressesCount = externalCount;

                var accountWithAddresses = _AccountsWithAddresses(@lock);

                foreach (var addr in account.GetReceiveAddress(account.GapLimit))
                {
                    var scriptHashStr = addr.ToScriptHash().ToHex();
                    var accountAddresses = _GetAccountAddresses(account);

                    var t = electrum.BlockchainScriptHashSubscribe(scriptHashStr, async (str) =>
                    {
                        var status = Deserialize<ResultAsString>(str);

                        if (!string.IsNullOrEmpty(status.Result))
                        {
                            Debug.WriteLine($"[Start] Subscribed to {status.Result}, for address: {addr.ToString()}");

                            var unspent = await electrum.BlockchainScriptHashListUnspent(scriptHashStr);

                            foreach (var unspentResult in unspent.Result)
                            {
                                var txHash = unspentResult.TxHash;
                                var height = unspentResult.Height;

                                var currentTx = account.Txs.FirstOrDefault((i) => i.Id.ToString() == txHash);

                                // Tx is new
                                if (currentTx is null)
                                {
                                    var blkChainTxGet = await electrum.BlockchainTransactionGet(txHash);
                                    var txHex = blkChainTxGet.Result;

                                    var tx = Tx.CreateFromHex(txHex, account, Network, height, accountAddresses["external"], accountAddresses["internal"]);

                                    account.AddTx(tx);

                                    if (tx.AccountId == CurrentAccountId)
                                        OnNewTransaction?.Invoke(this, tx);

                                    return;
                                }

                                // A potential update if tx heights are different
                                if (currentTx.BlockHeight != height)
                                {
                                    var blkChainTxGet = await electrum.BlockchainTransactionGet(txHash);
                                    var txHex = blkChainTxGet.Result;

                                    var tx = Tx.CreateFromHex(txHex, account, Network, height, accountAddresses["external"], accountAddresses["internal"]);

                                    account.UpdateTx(tx);

                                    if (tx.AccountId == CurrentAccountId)
                                        OnUpdateTransaction?.Invoke(this, tx);

                                    // Here for safety, at any time somebody can add code to this
                                    return;
                                }
                            }
                        }
                    });

                    tasks.Add(t);
                }

                account.ExternalAddressesCount = 0;
            }

            // Runs all the script hash suscribers
            await Task.WhenAll(tasks).WithCancellation(CancellationToken.None);
        }

        public async Task Sync()
        {
            Debug.WriteLine($"[Sync] Attempting to sync wallet with id: {Id}");
            SyncStarted?.Invoke(this, null);

            await _SyncTask();
        }

        public async Task Resync()
        {
            Debug.WriteLine($"[Resync] Attempting to resync wallet with id: {Id}");

            // First we do a cleanup so we can rediscover txs
            TxIds = new List<string> { };
            Txs = new List<Tx> { };

            var @lock = new object();
            foreach (var account in Accounts)
            {
                lock (@lock)
                {
                    account.InternalAddressesCount = 0;
                    account.ExternalAddressesCount = 0;

                    account.TxIds = new List<string> { };
                    account.Txs = new List<Tx> { };
                }
            }

            //  And we do the regular sync
            await Sync();
        }

        async Task _SyncTask()
        {
            var electrum = await GetElectrumClient();
            var @lock = new object();
            using (var cts = new CancellationTokenSource())
            {
                await Task.Factory.StartNew(async () =>
                {
                    Debug.WriteLine("[Sync] Syncing...");

                    // This is a data structure that's really useful to create a transaction
                    var accountsWithAddresses = _AccountsWithAddresses(@lock);

                    var tasks = new List<Task>();
                    foreach (KeyValuePair<IAccount, Dictionary<string, BitcoinAddress[]>> entry in accountsWithAddresses)
                    {
                        var t = _GetAccountTask(entry, electrum);

                        tasks.Add(t);
                    }

                    await Task.Factory.ContinueWhenAll(tasks.ToArray(), (completedTasks) =>
                    {
                        SyncFinished?.Invoke(this, null);
                    });
                }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        Task _GetAccountTask(KeyValuePair<IAccount, Dictionary<string, BitcoinAddress[]>> entry, ElectrumClient electrum)
        {
            var t = Task.Run(async () =>
            {
                var account = entry.Key;
                var addresses = new List<BitcoinAddress> { };

                addresses.AddRange(entry.Value["external"]);
                addresses.AddRange(entry.Value["internal"]);

                foreach (var addr in addresses)
                {
                    Debug.WriteLine($"[Sync] Trying to sync: {addr.ToString()}");

                    var scriptHashHex = addr.ToScriptHash().ToHex();
                    var historyRes = await electrum.BlockchainScriptHashGetHistory(scriptHashHex);

                    await _InsertTransactionsFromHistory(historyRes, account, electrum, entry);
                }
            });

            return t;
        }

        async Task _InsertTransactionsFromHistory(BlockchainScriptHashGetHistoryResult result, IAccount account, ElectrumClient electrum, KeyValuePair<IAccount, Dictionary<string, BitcoinAddress[]>> entry)
        {
            foreach (var r in result.Result)
            {
#if DEBUG
                // Upps... This is what happens when you test some bitcoin wallets,
                // this happened because I sent to a change address so the software thinks is a receive...
                // now I don't have a way to tell if the tx is receive or send...
                if (r.TxHash == "45f4d79ea7754cfdb3be338d1e5d674d6f7f4dc5a1c71867b68b647bed788d00")
                    continue;
#endif

                Debug.WriteLine($"[Sync] Found tx with hash: {r.TxHash}");

                var externalAddresses = entry.Value["external"];
                var internalAddresses = entry.Value["internal"];

                var txRes = await electrum.BlockchainTransactionGet(r.TxHash);

                var tx = Tx.CreateFromHex(
                    txRes.Result,
                    account,
                    Network,
                    r.Height,
                    externalAddresses,
                    internalAddresses
                );

                var txAddresses = Transaction.Parse(
                    tx.Hex,
                    Network
                ).Outputs.Select(
                    (o) => o.ScriptPubKey.GetDestinationAddress(Network)
                );

                if (TxIds.Contains(tx.Id.ToString()))
                {
                    account.UpdateTx(tx);
                }
                else
                {
                    account.AddTx(tx);
                }

                foreach (var txAddr in txAddresses)
                {
                    if (externalAddresses.Contains(txAddr))
                    {
                        if (account.UsedExternalAddresses.Contains(txAddr))
                            continue;

                        account.UsedExternalAddresses.Add(txAddr);
                    }

                    if (internalAddresses.Contains(txAddr))
                    {
                        if (account.UsedInternalAddresses.Contains(txAddr))
                            continue;

                        account.UsedInternalAddresses.Add(txAddr);
                    }
                }

            }
        }

        Dictionary<IAccount, Dictionary<string, BitcoinAddress[]>> _AccountsWithAddresses(object @lock = null)
        {
            @lock = @lock ?? new object();
            var accountsWithAddresses = new Dictionary<IAccount, Dictionary<string, BitcoinAddress[]>>();

            foreach (var account in Accounts)
            {
                var addresses = _GetAccountAddresses(account, @lock);

                accountsWithAddresses.Add(account, addresses);
            }

            return accountsWithAddresses;
        }

        Dictionary<string, BitcoinAddress[]> _GetAccountAddresses(IAccount account, object @lock = null)
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

        async Task<ElectrumClient> GetElectrumClient()
        {
            var recentServers = await GetRecentlyConnectedServers();

            Debug.WriteLine("[GetElectrumClient] Will connect to:");
            foreach (var server in recentServers)
            {
                Debug.WriteLine($"[GetElectrumClient] {server.Domain}:{server.PrivatePort} ({server.Version})");
            }

            return new ElectrumClient(recentServers);
        }

        async Task<List<Server>> GetRecentlyConnectedServers(bool retrying = false)
        {
            Debug.WriteLine("[GetRecentlyConnectedServers] Attempting to get the recent servers");

            var recentServers = ElectrumClient.GetRecentlyConnectedServers(Network);

            if (recentServers.Count == 0)
            {
                Debug.WriteLine("[GetRecentlyConnectedServers] Failed to fetch connected servers. Populating list");

                // Waits 2 seconds if we need to reconnect, only on retry
                if (retrying) await Task.Delay(TimeSpan.FromSeconds(2.0));

                ElectrumClient.PopulateRecentlyConnectedServers(Network, _Assembly);

                return await GetRecentlyConnectedServers(retrying: true);
            }

            Debug.WriteLine($"Found {recentServers.Count} servers to connect");

            return recentServers;
        }

        /// <summary>
        /// Read <see cref="AddAccount(string, string, object)"/>
        /// </summary>
        /// <param name="options">Check <see cref="AddAccount(string, string, object)"/> for an example</param>
        /// <returns></returns>
        IAccount NewAccount(string type, string name, object options)
        {
            Guard.NotEmpty(type, nameof(type));

            if (!AccountTypes.Contains(type))
                throw new ArgumentException($"Invalid account type: {type}");

            // TODO get this from the wallet I guess we need translations in Liviano as well
            if (string.IsNullOrEmpty(name) && Accounts.Count == 0)
                name = DEFAULT_ACCOUNT_NAME;

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid account name: It cannot be empty!");

            switch (type)
            {
                case "bip44":
                case "bip49":
                case "bip84":
                case "bip141":
                    return Bip32Account.Create(name, new { Wallet = this, Network, Type = type });
                case "wasabi":
                    return WasabiAccount.Create(name, options);
                case "paper":
                    return PaperAccount.Create(name, options);
            }

            return Bip32Account.Create(name, new { Wallet = this, Network, Type = "bip141" });
        }
    }
}
