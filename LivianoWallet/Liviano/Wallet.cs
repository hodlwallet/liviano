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

        Key privateKey;

        ExtKey extKey;

        public string[] AccountTypes => new string[] { "bip141", "bip44", "bip49", "bip84", "paper", "wasabi" };

        public string Id { get; set; }

        public string Name { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }

        public Network Network { get; set; }

        public string EncryptedSeed { get; set; }

        public byte[] ChainCode { get; set; }

        public string CurrentAccountId { get; set; }

        public Assembly CurrentAssembly { get; set; }

        IAccount currentAccount;
        public IAccount CurrentAccount
        {
            get
            {
                if (currentAccount is null || currentAccount.Id != CurrentAccountId)
                {
                    currentAccount = Accounts.FirstOrDefault((a) => a.Id == CurrentAccountId);
                }

                return currentAccount;
            }

            set
            {
                CurrentAccountId = value.Id;
                currentAccount = value;
            }
        }

        public List<string> TxIds { get; set; }
        public List<Tx> Txs { get; set; }

        public List<string> AccountIds { get; set; }
        public List<IAccount> Accounts { get; set; }

        public Dictionary<string, int> AccountsIndex { get; set; }

        public event EventHandler SyncStarted;
        public event EventHandler SyncFinished;

        public event EventHandler<Tx> OnNewTransaction;
        public event EventHandler<Tx> OnUpdateTransaction;

        IStorage storage;
        public IStorage Storage
        {
            get => storage;
            set
            {
                storage = value;

                storage.Id = Id;
                storage.Network = Network;
                storage.Wallet = this;
            }
        }

        public ElectrumPool ElectrumPool { get; set; }

        public string Server { get; set; }

        public void Init(string mnemonic, string password = "", string name = null, Network network = null, DateTimeOffset? createdAt = null, IStorage storage = null, Assembly assembly = null)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            Id ??= Guid.NewGuid().ToString();
            Name ??= name ?? DEFAULT_WALLET_NAME;

            Network ??= network ?? Network.Main;

            CreatedAt ??= createdAt ?? DateTimeOffset.UtcNow;

            Storage ??= storage ?? new FileSystemStorage(Id, Network);

            TxIds ??= new List<string>();
            Txs ??= new List<Tx>();

            AccountIds ??= new List<string>();
            Accounts ??= new List<IAccount>();

            CurrentAccountId ??= null;
            currentAccount ??= null;

            CurrentAssembly ??= assembly;

            var mnemonicObj = Hd.MnemonicFromString(mnemonic);
            var extKey = Hd.GetExtendedKey(mnemonicObj, password);

            EncryptedSeed = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network).ToWif();
            ChainCode = extKey.ChainCode;

            ElectrumPool ??= GetElectrumPool();
            privateKey ??= GetPrivateKey(password, decrypt: true);

            InitAccountsIndex();
        }

        public void InitAccountsIndex()
        {
            AccountsIndex = new Dictionary<string, int>();
            var types = new string[] { "bip44", "bip49", "bip84", "bip141", "wasabi", "paper" };

            foreach (var t in types)
            {
                AccountsIndex.Add(t, -1);
            }
        }

        /// <summary>
        /// Get an electrum pool loaded from the assembly or files.
        /// </summary>
        /// <returns>a <see cref="ElectrumPool"/></returns>
        ElectrumPool GetElectrumPool()
        {
            if (!(ElectrumPool is null)) return ElectrumPool;

            return ElectrumPool.Load(Network, CurrentAssembly);
        }

        public void InitPrivateKey(string password = "", bool decrypt = false)
        {
            privateKey = GetPrivateKey(password, decrypt);
        }

        /// <summary>
        /// Gets the private key, and puts it into <see cref="privateKey"/>
        /// </summary>
        /// <param name="password"></param>
        /// <param name="decrypt"></param>
        /// <returns>a private <see cref="Key"/></returns>
        public Key GetPrivateKey(string password = "", bool decrypt = false)
        {
            if (!(privateKey is null) && !decrypt) return privateKey;

            return Hd.DecryptSeed(EncryptedSeed, Network, password);
        }

        /// <summary>
        /// Gets the ext key and puts it into <see cref="extKey"/>
        /// </summary>
        /// <param name="password"></param>
        /// <param name="decrypt"></param>
        /// <returns></returns>
        public ExtKey GetExtendedKey(string password = "", bool decrypt = false)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(ChainCode, nameof(ChainCode));

            if (decrypt)
                privateKey = GetPrivateKey(password, decrypt);

            return new ExtKey(privateKey, ChainCode);
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

            account.Index = ++AccountsIndex[type];

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

                var accountWithAddresses = AccountsWithAddresses(@lock);

                foreach (var addr in account.GetReceiveAddress(account.GapLimit))
                {
                    var scriptHashStr = addr.ToScriptHash().ToHex();
                    var accountAddresses = GetAccountAddresses(account);

                    var t = electrum.BlockchainScriptHashSubscribe(scriptHashStr, async (str) =>
                    {
                        var status = Deserialize<ResultAsString>(str);

                        if (!string.IsNullOrEmpty(status.Result))
                        {
                            Debug.WriteLine($"[Start] Subscribed to {status.Result}, for address: {addr.ToString()}");

                            try
                            {
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
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Start] There was an error gathering UTXOs: {ex.Message}");
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

            try
            {
                await SyncTask();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"There was an error during sync: {ex.Message}");
            }
        }

        public async Task Resync()
        {
            Debug.WriteLine($"[Resync] Attempting to resync wallet with id: {Id}");

            Cleanup();

            //  And we do the regular sync
            await Sync();
        }

        public void Cleanup()
        {
            Debug.WriteLine($"[Cleanup] Attempting to clean wallet with id: {Id}");

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
        }

        async Task SyncTask()
        {
            Debug.WriteLine("[Sync] Syncing...");

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            await ElectrumPool.SyncWallet(this, ct);

            ElectrumPool.OnNewTransaction += (o, tx) =>
            {
                Console.WriteLine($"Found a tx! hex: {tx.Hex}");
            };
        }

        public void UpdateCurrentTransaction(Tx tx)
        {
            if (CurrentAccount.TxIds.Contains(tx.Id.ToString()))
            {
                CurrentAccount.UpdateTx(tx);
                OnUpdateTransaction?.Invoke(this, tx);
            }
        }

        public async Task<(bool Sent, string Error)> SendTransaction(Transaction tx)
        {
            var txHex = tx.ToHex();

            Debug.WriteLine($"[Send] Attempting to send a transaction: {txHex}");

            try
            {
                var electrum = await GetElectrumClient();
                var broadcast = await electrum.BlockchainTransactionBroadcast(txHex);

                if (broadcast.Result != tx.GetHash().ToString())
                {
                    throw new Exception($"Transaction Broadcast failed for tx: {txHex}\n{broadcast.Result}");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[Error] {e.Message}");

                return (false, e.Message);
            }

            return (true, null);
        }

        Dictionary<IAccount, Dictionary<string, BitcoinAddress[]>> AccountsWithAddresses(object @lock = null)
        {
            @lock = @lock ?? new object();
            var accountsWithAddresses = new Dictionary<IAccount, Dictionary<string, BitcoinAddress[]>>();

            foreach (var account in Accounts)
            {
                var addresses = GetAccountAddresses(account, @lock);

                accountsWithAddresses.Add(account, addresses);
            }

            return accountsWithAddresses;
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

        async Task<ElectrumClient> GetElectrumClient()
        {
            var recentServers = await GetRecentlyConnectedServers();

            return recentServers[0].ElectrumClient;
        }

        async Task<List<Server>> GetRecentlyConnectedServers(bool retrying = false)
        {
            Debug.WriteLine("[GetRecentlyConnectedServers] Attempting to get the recent servers");

            var recentServers = ElectrumPool.GetRecentServers(Network);

            if (recentServers.Count == 0)
            {
                Debug.WriteLine("[GetRecentlyConnectedServers] Failed to fetch connected servers. Populating list");

                // Waits 2 seconds if we need to reconnect, only on retry
                if (retrying) await Task.Delay(TimeSpan.FromSeconds(2.0));

                var name = $"Resources.Electrum.servers.{Network.Name.ToLower()}.json";
                //using (var stream = CurrentAssembly.GetManifestResourceStream(name))
                //{
                //    ElectrumClient.PopulateRecentlyConnectedServers(stream, Network);
                //}

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

            var index = GetAccountsIndex(type) + 1;
            switch (type)
            {
                case "bip44":
                case "bip49":
                case "bip84":
                case "bip141":
                    return Bip32Account.Create(name, new { Wallet = this, Network, Type = type, Index = index });
                case "wasabi":
                    return WasabiAccount.Create(name, options); // TODO Express acocunt index
                case "paper":
                    return PaperAccount.Create(name, options); // TODO Express acocunt index
                default:
                    return Bip32Account.Create(name, new { Wallet = this, Network, Type = "bip141", Index = index });
            }
        }

        int GetAccountsIndex(string type)
        {
            if (!AccountsIndex.Keys.Contains(type))
                throw new ArgumentException($"Invalid account type: {type}");

            return AccountsIndex[type];
        }
    }
}
