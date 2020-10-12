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
        const string DEFAULT_WALLET_NAME = "Bitcoin Wallet";
        const string DEFAULT_ACCOUNT_NAME = "Bitcoin Account";

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
        public event EventHandler<WatchAddressEventArgs> OnWatchAddressNotified;

        public event EventHandler<TxEventArgs> OnNewTransaction;
        public event EventHandler<TxEventArgs> OnUpdateTransaction;

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

        public ElectrumPool ElectrumPool { get; set; }

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
        public ElectrumPool GetElectrumPool()
        {
            if (!(ElectrumPool is null))
            {
                return ElectrumPool;
            }

            return ElectrumPool.Load(Network);
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
        /// Sync wallet
        /// </summary>
        public async Task Sync()
        {
            Debug.WriteLine($"[Sync] Attempting to sync wallet with id: {Id}");

            try
            {
                await SyncTask();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"There was an error during sync: {ex.Message}");
                throw ex;
            }
        }

        /// <summary>
        /// Resync wallet, clean and sync
        /// </summary>
        public async Task Resync()
        {
            Debug.WriteLine($"[Resync] Attempting to resync wallet with id: {Id}");

            Cleanup();

            //  And we do the regular sync
            await Sync();
        }

        public async Task Watch()
        {
            Debug.WriteLine("[Watch] Watch started...");

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            ElectrumPool.OnNewTransaction += ElectrumPool_OnNewTransaction;
            ElectrumPool.OnUpdateTransaction += ElectrumPool_OnUpdateTransaction;
            ElectrumPool.OnWatchStarted += ElectrumPool_OnWatchStarted;

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

            if (!ElectrumPool.Connected) await ElectrumPool.FindConnectedServersUntilMinNumber(cts);
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
                    account.InternalAddressesCount = 0;
                    account.ExternalAddressesCount = 0;

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

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

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
                await ElectrumPool.FindConnectedServersUntilMinNumber(cts);
        }

        private async Task ElectrumPool_OnConnectedToSync(object sender, Server server, CancellationToken ct)
        {
            Debug.WriteLine($"[ElectrumPool_OnConnectedToSync] Sent from {sender}");

            Console.WriteLine($"Connected to {server.Domain}, recently connected server.");
            Console.WriteLine($"Now starts to sync wallet");
            Console.WriteLine();

            await ElectrumPool.SyncWallet(this, ct);
        }

        private async Task ElectrumPool_OnConnectedToWatch(object sender, Server server, CancellationToken ct)
        {
            Debug.WriteLine($"[ElectrumPool_OnConnectedToWatch] Sent from {sender}");
            Debug.WriteLine($"[ElectrumPool_OnConnectedToWatch] Server {server.Domain}");

            Console.WriteLine($"Now starts to watch wallet");

            ElectrumPool.OnWatchAddressNotified += (o, args) =>
            {
                Debug.WriteLine("[ElectrumPool_OnConnectedToWatch] Found a status!");

                OnWatchAddressNotified?.Invoke(this, args);
            };

            await ElectrumPool.WatchWallet(this, ct);
        }

        private void ElectrumPool_OnSyncStarted(object sender, EventArgs args)
        {
            Console.WriteLine($"Sync started at {DateTime.Now}");

            this.OnSyncStarted?.Invoke(this, null);
        }

        private void ElectrumPool_OnWatchStarted(object sender, EventArgs args)
        {
            Console.WriteLine($"Watch started at {DateTime.Now}");

            this.OnWatchStarted?.Invoke(this, null);
        }

        private void ElectrumPool_OnSyncFinished(object sender, EventArgs args)
        {
            Console.WriteLine($"Sync finished at {DateTime.Now}");

            this.OnSyncFinished?.Invoke(this, args);
        }

        private void ElectrumPool_OnNewTransaction(object sender, TxEventArgs txArgs)
        {
            var tx = txArgs.Tx;
            var addr = txArgs.Address;
            var acc = txArgs.Account;

            acc.AddTx(tx);

            Debug.WriteLine($"[ElectrumPool_OnNewTransaction] Found a tx! tx_id: {tx.Id}");
            Debug.WriteLine($"[ElectrumPool_OnNewTransaction]             addr:  {addr}");

            var transaction = Transaction.Parse(tx.Hex, acc.Network);
            var addresses = acc.GetAddressesToWatch();

            foreach (var coin in transaction.Outputs.AsCoins())
            {
                var destinationAddress = coin.TxOut.ScriptPubKey.GetDestinationAddress(acc.Network);

                if (addresses.Contains(destinationAddress)) acc.AddUtxo(coin);
            }

            Storage.Save();

            OnNewTransaction?.Invoke(this, txArgs);
        }

        private void ElectrumPool_OnUpdateTransaction(object sender, TxEventArgs txArgs)
        {
            var tx = txArgs.Tx;
            var addr = txArgs.Address;
            var acc = txArgs.Account;

            acc.UpdateTx(tx);

            Debug.WriteLine($"[ElectrumPool_OnUpdateTransaction] Updating a tx! tx_id: {tx.Id}");
            Debug.WriteLine($"[ElectrumPool_OnUpdateTransaction]               addr:  {addr}");

            // In case the updated transaction includes a new utxo for me
            var transaction = Transaction.Parse(tx.Hex, acc.Network);
            var addresses = acc.GetAddressesToWatch();

            foreach (var coin in transaction.Outputs.AsCoins())
            {
                var destinationAddress = coin.TxOut.ScriptPubKey.GetDestinationAddress(acc.Network);

                if (addresses.Contains(destinationAddress)) acc.AddUtxo(coin);
            }

            Storage.Save();

            OnUpdateTransaction?.Invoke(this, txArgs);
        }

        public async Task<(bool Sent, string Error)> SendTransaction(Transaction tx)
        {
            var txHex = tx.ToHex();

            Debug.WriteLine($"[Send] Attempting to send a transaction: {txHex}");

            try
            {
                var electrum = ElectrumPool.CurrentServer.ElectrumClient;
                var broadcast = await electrum.BlockchainTransactionBroadcast(txHex);

                if (broadcast.Result != tx.GetHash().ToString())
                {
                    throw new WalletException($"Transaction Broadcast failed for tx: {txHex}\n{broadcast.Result}");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[Error] {e.Message}");

                return (false, e.Message);
            }

            return (true, null);
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

            switch (type)
            {
                case "bip44":
                case "bip49":
                case "bip84":
                case "bip141":
                    return Bip32Account.Create(name, new { Wallet = this, Network, Type = type, Index = index });
                case "paper":
                    return PaperAccount.Create(name, options);
                default:
                    return Bip32Account.Create(name, new { Wallet = this, Network, Type = "bip84", Index = index });
            }
        }

        public (Transaction transaction, string error) CreateTransaction(IAccount account, string destinationAddress, double amount, int feeSatsPerByte, string passphrase = null)
        {
            Transaction tx = null;
            string error = null;
            var txAmount = new Money(new Decimal(amount), MoneyUnit.BTC);

            try
            {
                tx = TransactionExtensions.CreateTransaction(destinationAddress, txAmount, (long)feeSatsPerByte, account);
            }
            catch (WalletException err)
            {
                Debug.WriteLine($"[CreateTransaction] Error: {err.Message}");

                return (tx, err.Message);
            }

            TransactionExtensions.VerifyTransaction(account, tx, out var errors);

            if (errors.Any())
            {
                error = string.Join<string>(", ", errors.Select(o => o.Message));

                Debug.WriteLine($"[CreateTransaction] Error: {error}");

                return (tx, error);
            }

            return (tx, null);
        }

        public async Task<bool> BroadcastTransaction(Transaction tx)
        {
            var res = await ElectrumPool.BroadcastTransaction(tx);

            return res;
        }
    }
}
