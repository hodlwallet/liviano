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
using Liviano.Exceptions;
using Liviano.Events;

namespace Liviano
{
    public class Wallet : IWallet
    {
        const string DEFAULT_WALLET_NAME = "Wallet";
        const string DEFAULT_ACCOUNT_NAME = "Account";

        public string[] AccountTypes => new string[] { "bip141", "bip44", "bip49", "bip84", "paper", "wasabi" };

        public string Id { get; set; }

        public string Name { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }

        public Network Network { get; set; }

        public string Seed { get; set; }

        public BitcoinExtKey MasterExtKey { get; set; }

        public BitcoinExtPubKey MasterExtPubKey { get; set; }

        public string EncryptedSeed { get; set; }

        public byte[] ChainCode { get; set; }

        public BlockHeader LastBlockHeader { get; set; }

        public string LastBlockHeaderHex { get; set; }

        public long Height { get; set; }

        public string CurrentAccountId { get; set; }

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

        public List<string> AccountIds { get; set; }
        public List<IAccount> Accounts { get; set; }

        public Dictionary<string, int> AccountsIndex { get; set; }

        public event EventHandler OnSyncStarted;
        public event EventHandler OnSyncFinished;
        public event EventHandler OnWatchStarted;

        public event EventHandler<TxEventArgs> OnNewTransaction;
        public event EventHandler<TxEventArgs> OnUpdateTransaction;

        public event EventHandler<WatchAddressEventArgs> OnWatchAddressNotified;
        public event EventHandler<NewHeaderEventArgs> OnNewHeaderNotified;

        IWalletStorage storage;
        public IWalletStorage Storage
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

        public CancellationTokenSource Cts { get; set; }

        public IElectrumPool ElectrumPool { get; set; }

        public string Server { get; set; }

        /// <summary>
        /// Inits the wallet with some defaults mostly empty fields
        /// </summary>
        /// <param name="mnemonic">A <see cref="string"/> with the mnemonic words</param>
        /// <param name="passphrase">A <see cref="string"/> with passphrase</param>
        /// <param name="name">A <see cref="string"/> with name</param>
        /// <param name="network">A <see cref="Network"/></param>
        /// <param name="createdAt">A <see cref="DateTimeOffset"/> of the time it was created</param>
        /// <param name="storage">A <see cref="IWalletStorage"/> that will store the wallet</param>
        public void Init(
                string mnemonic,
                string passphrase = null,
                string name = null,
                Network network = null,
                DateTimeOffset? createdAt = null,
                IWalletStorage storage = null,
                bool skipAuth = false)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            Id ??= Guid.NewGuid().ToString();
            Name ??= name ?? DEFAULT_WALLET_NAME;

            Network ??= network ?? Network.Main;

            CreatedAt ??= createdAt ?? DateTimeOffset.UtcNow;

            Storage ??= storage ?? new FileSystemWalletStorage(Id, Network);

            AccountIds ??= new List<string>();
            Accounts ??= new List<IAccount>();

            CurrentAccountId ??= null;
            currentAccount ??= null;

            Cts ??= new CancellationTokenSource();

            Seed = mnemonic;

            CreateAccountIndexes();
            if (!skipAuth) Authenticate(passphrase);
        }

        /// <summary>
        /// Authenticate and init privete keys
        /// </summary>
        /// <param name="passphrase">Passphrase</param>
        public bool Authenticate(string passphrase = null)
        {
            if (passphrase is null) passphrase = "";

            // This private key isn't validated yet
            var mnemonic = Hd.MnemonicFromString(Seed);
            var extKey = Hd.GetWif(Hd.GetExtendedKey(mnemonic, passphrase), Network);
            var extPubKey = extKey.Neuter();

            MasterExtKey = extKey;

            if (MasterExtPubKey is null)  // Create
            {
                MasterExtPubKey = extPubKey;

                Debug.WriteLine(
                    "[Authenticate] Success! We're creating a wallet with new passphrase!"
                );
            }
            else if (!extPubKey.Equals(MasterExtPubKey))
            {
                Debug.WriteLine(
                    "[Authenticate] Fail! Invalid passphrase, MasterExtKey will be reset to null!"
                );

                MasterExtKey = null;

                return false;
            }

            // Valid passphrase or creating

            Debug.WriteLine("[Authenticate] Success! Passphrase is correct!");

            EncryptedSeed = extKey.PrivateKey.GetEncryptedBitcoinSecret(
                passphrase, Network
            ).ToWif();
            ChainCode = extKey.ExtKey.ChainCode;

            return true;
        }

        /// <summary>
        /// Starts the possible accounts with indexes, these are {type_string: int_amount_of_accounts}
        /// </summary>
        public void CreateAccountIndexes()
        {
            if (!(AccountsIndex is null)) return;

            AccountsIndex = new Dictionary<string, int>();
            var types = new string[] { "bip44", "bip49", "bip84", "bip141", "paper" };

            foreach (var t in types)
            {
                AccountsIndex.Add(t, -1);
            }
        }

        /// <summary>
        /// Inits the electrum pool and subscribe to the handle connected servers if it is connected
        /// </summary>
        public void InitElectrumPool()
        {
            ElectrumPool ??= GetElectrumPool();

            // The event wont be invoked if it's null at first load because it wont have
            // an method to be attached to, this is why HandleConnectedServers ended up being public
            if (ElectrumPool.Connected)
                ElectrumPool.HandleConnectedServers(ElectrumPool.CurrentServer, null);
        }

        /// <summary>
        /// Get an electrum pool loaded from the assembly or files.
        /// </summary>
        /// <returns>a <see cref="ElectrumPool"/></returns>
        public IElectrumPool GetElectrumPool()
        {
            if (!(ElectrumPool is null))
            {
                return ElectrumPool;
            }

            return TrustedServer.Load(Network);
        }

        /// <summary>
        /// Gets the private key, and puts it into <see cref="privateKey"/>
        /// </summary>
        /// <param name="passphrase">A <see cref="string"/> of the passphrase</param>
        /// <returns>a private <see cref="Key"/></returns>
        public Key GetPrivateKey(string passphrase = null)
        {
            if (passphrase is null) passphrase = "";

            return Hd.DecryptSeed(EncryptedSeed, Network, passphrase);
        }

        /// <summary>
        /// Gets the ext key and puts it into <see cref="extKey"/>
        /// </summary>
        /// <param name="passphrase"></param>
        /// <param name="decrypt"></param>
        /// <returns></returns>
        public BitcoinExtKey GetExtendedKey(string passphrase = null)
        {
            Guard.NotNull(ChainCode, nameof(ChainCode));

            if (MasterExtKey != null) return MasterExtKey;

            var pk = new ExtKey(GetPrivateKey(passphrase), ChainCode);
            var pubkeyWif = pk.Neuter().GetWif(Network).ToString();

            if (!string.Equals(pubkeyWif, MasterExtPubKey))
                throw new WalletException("Not the right password chief.");

            return new BitcoinExtKey(pk, Network);
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

        /// <summary>
        /// Sync current account
        /// </summary>
        public async Task Sync()
        {
            Debug.WriteLine($"[Sync] Wallet id: {Id}");
            Debug.WriteLine($"[Sync] CurrentAccount id: {CurrentAccount.Id}");

            try
            {
                await SyncTask();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sync] There was an error during sync: {ex.Message}");
                Debug.WriteLine($"[Sync] Stacktrace: '{ex.StackTrace}'");
                throw;
            }
        }

        /// <summary>
        /// Resync wallet, clean and sync
        /// </summary>
        public async Task Resync()
        {
            Debug.WriteLine($"[Resync] Resyncing wallet with id: {Id}");

            Cleanup();

            //  And we do the regular sync
            await Sync();
        }

        public async Task Watch()
        {
            Debug.WriteLine("[Watch] Watch started...");

            var ct = Cts.Token;

            ElectrumPool.OnNewTransaction += ElectrumPool_OnNewTransaction;
            ElectrumPool.OnUpdateTransaction += ElectrumPool_OnUpdateTransaction;
            ElectrumPool.OnWatchStarted += ElectrumPool_OnWatchStarted;
            ElectrumPool.OnNewHeaderNotified += ElectrumPool_OnNewHeaderNotified;

            if (ElectrumPool.Connected)
                await ElectrumPool_OnConnectedToWatch(ElectrumPool, ElectrumPool.CurrentServer, ct);
            else
                ElectrumPool.OnConnected += async (o, server) =>
                {
                    await ElectrumPool_OnConnectedToWatch(
                        ElectrumPool,
                        server,
                        ct
                    );
                };

            if (!ElectrumPool.Connected) await ElectrumPool.Connect(3, Cts); // TODO use const for retries
        }

        /// <summary>
        /// Subscribe to headers
        /// </summary>
        public async Task SubscribeToHeaders()
        {
            var ct = Cts.Token;

            await ElectrumPool.SubscribeToHeaders(this, ct);
        }

        /// <summary>
        /// Cleanup wallet, set internal / external addr acocunts to 0
        /// </summary>
        public void Cleanup()
        {
            Debug.WriteLine($"[Cleanup] Attempting to clean wallet with id: {Id}");

            // First we do a cleanup so we can rediscover txs
            var @lock = new object();
            foreach (var account in Accounts)
            {
                lock (@lock)
                {
                    account.InternalAddressesGapIndex = 0;
                    account.ExternalAddressesGapIndex = 0;

                    account.InternalAddressesIndex = 0;
                    account.ExternalAddressesIndex = 0;

                    account.UsedExternalAddresses = new List<BitcoinAddress> { };
                    account.UsedInternalAddresses = new List<BitcoinAddress> { };

                    account.UnspentCoins = new List<Coin> { };
                    account.SpentCoins = new List<Coin> { };

                    account.TxIds = new List<string> { };
                    account.Txs = new List<Tx> { };
                }
            }
        }

        /// <summary>
        /// Helper for sync
        /// </summary>
        async Task SyncTask()
        {
            Debug.WriteLine("[Sync] Syncing...");

            var ct = Cts.Token;

            ElectrumPool.OnNewTransaction += ElectrumPool_OnNewTransaction;
            ElectrumPool.OnUpdateTransaction += ElectrumPool_OnUpdateTransaction;
            ElectrumPool.OnSyncStarted += ElectrumPool_OnSyncStarted;
            ElectrumPool.OnSyncFinished += ElectrumPool_OnSyncFinished;

            if (ElectrumPool.Connected)
                await ElectrumPool_OnConnectedToSync(ElectrumPool, ElectrumPool.CurrentServer, ct);
            else
                ElectrumPool.OnConnected += async (o, server) => await ElectrumPool_OnConnectedToSync(
                    ElectrumPool,
                    server,
                    ct
                );

            if (!ElectrumPool.Connected)
                await ElectrumPool.Connect(3, Cts); // TODO use a const for retries
        }

        async Task ElectrumPool_OnConnectedToSync(object sender, Server server, CancellationToken ct)
        {
            Debug.WriteLine($"[ElectrumPool_OnConnectedToSync] Sent from {sender}");

            Debug.WriteLine($"[ElectrumPool_OnConnectedToSync] Connected to {server.Domain}, recently connected server.");
            Debug.WriteLine($"[ElectrumPool_OnConnectedToSync] Now starts to sync wallet");

            Debug.WriteLine("");

            _ = ElectrumPool.PeriodicPing(ElectrumServer_PingSuccessCallback, ElectrumServer_PingFailedCallback, null);
            _ = ElectrumPool.SubscribeToHeaders(this, ct);

            await ElectrumPool.SyncAccount(CurrentAccount, ct);
        }

        async Task ElectrumPool_OnConnectedToWatch(object sender, Server server, CancellationToken ct)
        {
            Debug.WriteLine($"[ElectrumPool_OnConnectedToWatch] Sent from {sender}");
            Debug.WriteLine($"[ElectrumPool_OnConnectedToWatch] Server {server.Domain}");

            Debug.WriteLine($"[ElectrumPool_OnConnectedToWatch] Now starts to watch wallet");

            ElectrumPool.OnWatchAddressNotified += (o, args) =>
            {
                Debug.WriteLine("[ElectrumPool_OnConnectedToWatch] Found a status!");

                OnWatchAddressNotified?.Invoke(this, args);
            };

            _ = ElectrumPool.PeriodicPing(ElectrumServer_PingSuccessCallback, ElectrumServer_PingFailedCallback, null);
            _ = ElectrumPool.SubscribeToHeaders(this, ct);

            await ElectrumPool.WatchAccount(CurrentAccount, ct);
        }

        void ElectrumServer_PingSuccessCallback(DateTimeOffset? pingFailedAt)
        {
            Debug.WriteLine($"[Connect] Ping successful at {pingFailedAt}.");
        }

        async void ElectrumServer_PingFailedCallback(DateTimeOffset? pingFailedAt)
        {
            Debug.WriteLine($"[Connect] Ping failed at {pingFailedAt}. Reconnecting...");
            ElectrumPool.CurrentServer.OnConnectedEvent = null;

            await Task.Delay(TrustedServer.RECONNECT_DELAY);
            await ElectrumPool.Connect(3, Cts); // TODO use a const for retries
        }

        void ElectrumPool_OnSyncStarted(object sender, EventArgs args)
        {
            Debug.WriteLine($"[ElectrumPool_OnSyncStarted] Sync started at {DateTime.Now}");

            this.OnSyncStarted?.Invoke(this, null);
        }

        void ElectrumPool_OnNewHeaderNotified(object sender, NewHeaderEventArgs args)
        {
            Debug.WriteLine($"[ElectrumPool_OnSyncStarted] Sync started at {DateTime.Now}");

            // TODO Here some code should be needed that calls each account and
            // go tru each tx so it can update their confirmations, but invoking is great as well

            this.OnNewHeaderNotified?.Invoke(this, args);
        }

        void ElectrumPool_OnWatchStarted(object sender, EventArgs args)
        {
            Debug.WriteLine($"[ElectrumPool_OnWatchStarted] Watch started at {DateTime.Now}");

            this.OnWatchStarted?.Invoke(this, null);
        }

        void ElectrumPool_OnSyncFinished(object sender, EventArgs args)
        {
            Debug.WriteLine($"[ElectrumPool_OnSyncFinished] Sync finished at {DateTime.Now}");

            this.OnSyncFinished?.Invoke(this, args);
        }

        void ElectrumPool_OnNewTransaction(object sender, TxEventArgs txArgs)
        {
            var tx = txArgs.Tx;
            var addr = txArgs.Address;
            var acc = txArgs.Account;

            acc.AddTx(tx);

            Debug.WriteLine($"[ElectrumPool_OnNewTransaction] Found a tx! tx_id: {tx.Id}");
            Debug.WriteLine($"[ElectrumPool_OnNewTransaction]             addr:  {addr}");

            var transaction = Transaction.Parse(tx.Hex, acc.Network);

            foreach (var coin in transaction.Outputs.AsCoins())
            {
                var destinationAddress = coin.TxOut.ScriptPubKey.GetDestinationAddress(acc.Network);

                if (tx.Account.IsReceive(destinationAddress) || tx.Account.IsChange(destinationAddress)) acc.AddUtxo(coin);
            }

            OnNewTransaction?.Invoke(this, txArgs);

            Storage.Save();
        }

        void ElectrumPool_OnUpdateTransaction(object sender, TxEventArgs txArgs)
        {
            var tx = txArgs.Tx;
            var addr = txArgs.Address;
            var acc = txArgs.Account;

            acc.UpdateTx(tx);

            Debug.WriteLine($"[ElectrumPool_OnUpdateTransaction] Updating a tx! tx_id: {tx.Id}");
            Debug.WriteLine($"[ElectrumPool_OnUpdateTransaction]               addr:  {addr}");

            // In case the updated transaction includes a new utxo for me
            var transaction = Transaction.Parse(tx.Hex, acc.Network);

            foreach (var coin in transaction.Outputs.AsCoins())
            {
                var destinationAddress = coin.TxOut.ScriptPubKey.GetDestinationAddress(acc.Network);

                if (tx.Account.IsReceive(destinationAddress) || tx.Account.IsChange(destinationAddress)) acc.AddUtxo(coin);
            }

            OnUpdateTransaction?.Invoke(this, txArgs);

            Storage.Save();
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

            if (string.IsNullOrEmpty(name) && Accounts.Count == 0)
                name = DEFAULT_ACCOUNT_NAME;

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid account name: It cannot be empty!");

            var index = ++AccountsIndex[type];

            return type switch
            {
                "bip44" or "bip49" or "bip84" or "bip141" => Bip32Account.Create(name, new { Wallet = this, Network, Type = type, Index = index }),
                "paper" => PaperAccount.Create(name, options),
                _ => Bip32Account.Create(name, new { Wallet = this, Network, Type = "bip84", Index = index }),
            };
        }

        public (Transaction transaction, string error) CreateTransaction(IAccount account, string destinationAddress, double amount, decimal feeSatsPerByte, bool rbf, string passphrase = null)
        {
            Transaction tx = null;
            var txAmount = new Money(new decimal(amount), MoneyUnit.BTC);

            try
            {
                string error;
                (tx, error) = account.CreateTransaction(destinationAddress, txAmount, feeSatsPerByte, rbf);
            }
            catch (WalletException err)
            {
                Debug.WriteLine($"[CreateTransaction] Error: {err.Message}");

                return (tx, err.Message);
            }

            return (tx, null);
        }

        public async Task<(bool Result, string Error)> Broadcast(Transaction tx)
        {
            var res = await ElectrumPool.Broadcast(tx);

            return res;
        }
    }
}
