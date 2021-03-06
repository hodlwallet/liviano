//
// IWallet.cs
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
using System.Threading;
using System.Threading.Tasks;

using NBitcoin;

using Newtonsoft.Json;

using Liviano.Events;
using Liviano.Models;

namespace Liviano.Interfaces
{
    public interface IWallet
    {
        /// <summary>
        /// A list of types of accounts, e.g. "bip44", "bip141"...
        /// </summary>
        [JsonIgnore]
        string[] AccountTypes { get; }

        /// <summary>
        /// Id of the wallet, this will be in the filesystem
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "id")]
        string Id { get; }

        /// <summary>
        /// Name to show for the wallet, probably user provided or default
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        [JsonProperty(PropertyName = "createdAt", NullValueHandling = NullValueHandling.Ignore)]
        DateTimeOffset? CreatedAt { get; set; }

        /// <summary>
        /// The network this wallets belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        Network Network { get; set; }

        /// <summary>
        /// Mnemonic seed as a string
        /// </summary>
        [JsonProperty(PropertyName = "seed")]
        string Seed { get; set; }

        /// <summary>
        /// Master Extended Private Key
        /// </summary>
        [JsonIgnore]
        BitcoinExtKey MasterExtKey { get; set; }

        /// <summary>
        /// Master Extended Pub Key
        /// </summary>
        [JsonProperty(PropertyName = "masterExtPubKey")]
        BitcoinExtPubKey MasterExtPubKey { get; set; }

        /// <summary>
        /// Encrypted seed usually from a mnemonic
        /// </summary>
        [JsonIgnore]
        string EncryptedSeed { get; set; }

        /// <summary>
        /// The chain code.
        /// </summary>
        [JsonIgnore]
        byte[] ChainCode { get; set; }

        /// <summary>
        /// The Sync status
        /// </summary>
        [JsonProperty(PropertyName = "syncStatus", NullValueHandling = NullValueHandling.Ignore)]
        SyncStatus SyncStatus { get; set; }

        /// <summary>
        /// The last block header
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockHeader", NullValueHandling = NullValueHandling.Ignore)]
        BlockHeader LastBlockHeader { get; set; }

        /// <summary>
        /// The block header hex
        /// </summary>
        [JsonIgnore]
        string LastBlockHeaderHex { get; set; }

        /// <summary>
        /// The block height
        /// </summary>
        [JsonProperty(PropertyName = "height", NullValueHandling = NullValueHandling.Ignore)]
        long Height { get; set; }

        /// <summary>
        /// Current account the wallet is on
        /// </summary>
        [JsonProperty(PropertyName = "currentAccountId", NullValueHandling = NullValueHandling.Ignore)]
        string CurrentAccountId { get; set; }

        /// <summary>
        /// Stores the account object pointed to from currentAccountId
        /// </summary>
        [JsonIgnore]
        IAccount CurrentAccount { get; set; }

        /// <summary>
        /// Account ids of the wallet, these go under {walletId}/accounts
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "accountIds", DefaultValueHandling = DefaultValueHandling.Ignore)]
        List<string> AccountIds { get; set; }

        /// <summary>
        /// All the accounts objects
        /// </summary>
        [JsonIgnore]
        List<IAccount> Accounts { get; set; }

        /// <summary>
        /// Get account indexes
        /// </summary>
        [JsonProperty(PropertyName = "accountsIndex", DefaultValueHandling = DefaultValueHandling.Ignore)]
        Dictionary<string, int> AccountsIndex { get; set; }

        [JsonIgnore]
        IWalletStorage Storage { get; set; }

        [JsonIgnore]
        CancellationTokenSource Cts { get; set; }

        [JsonProperty(PropertyName = "server", DefaultValueHandling = DefaultValueHandling.Ignore)]
        string Server { get; set; }

        [JsonIgnore]
        IElectrumPool ElectrumPool { get; set; }

        /// <summary>
        /// Init will create a new wallet initaliaing everything to their defaults,
        /// a new guid is created and the default for network is Main
        /// </summary>
        void Init(
            string mnemonic,
            string passphrase = "",
            string name = null,
            Network network = null,
            DateTimeOffset? createdAt = null,
            IWalletStorage storage = null,
            bool skipAuth = false
        );

        void InitElectrumPool();

        /// <summary>
        /// Authenticate the wallet with a passphrase
        /// </summary>
        /// <param name="passphrase"></param>
        /// <returns>True if it succeeded false if it doesn't</returns>
        bool Authenticate(string passphrase = null);

        /// <summary>
        /// Starts all the indexes of all types of accounts tracked
        /// </summary>
        void CreateAccountIndexes();

        /// <summary>
        /// Finds all the possible accounts in the wallet
        /// </summary>
        List<IAccount> FindAccounts();

        /// <summary>
        /// Syncing, used to start syncing from 0 and also to continue after booting
        /// </summary>
        /// <returns></returns>
        Task Sync();

        /// <summary>
        /// Resyncing is done to start from 0 always, and discover the HD accounts attached to it.
        /// </summary>
        /// <returns></returns>
        Task Resync();

        /// <summary>
        /// Watches a wallet for new transactions
        /// </summary>
        /// <returns></returns>
        Task Watch();

        /// <summary>
        /// Gets a private key this method also caches it on memory
        /// </summary>
        /// <param name="passphrase">Passphrase to decrypt seed to, default ""</param>
        /// <returns></returns>
        Key GetPrivateKey(string passphrase = null);

        /// <summary>
        /// Gets the electrum pool from the network
        /// </summary>
        IElectrumPool GetElectrumPool();

        /// <summary>
        /// Gets a extended private key this method also caches it on memory
        /// </summary>
        /// <param name="passphrase">A passphrase</param>
        /// <returns></returns>
        BitcoinExtKey GetExtendedKey(string passphrase = null);

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
        void AddAccount(string type = "", string name = null, object options = null);

        /// <summary>
        /// Create transaction
        /// </summary>
        (Transaction transaction, string error) CreateTransaction(IAccount account, string destinationAddress, double amount, decimal feeSatsPerByte, bool rbf, string passphrase = "");

        /// <summary>
        /// Broadcast a transaction using the electrum client
        /// </summary>
        /// <param name="tx">Transaction to broadcast</param>
        /// <returns></returns>
        Task<(bool Result, string Error)> Broadcast(Transaction tx);

        /// <summary>
        /// Event handlers for syncing, start and end...
        /// </summary>
        event EventHandler OnCurrentAccountChanged;
        event EventHandler OnSyncStarted;
        event EventHandler OnSyncFinished;
        event EventHandler OnWatchStarted;

        event EventHandler OnSyncStatusChanged;

        event EventHandler<TxEventArgs> OnNewTransaction;
        event EventHandler<TxEventArgs> OnUpdateTransaction;

        event EventHandler<WatchAddressEventArgs> OnWatchAddressNotified;
        event EventHandler<NewHeaderEventArgs> OnNewHeaderNotified;

        event EventHandler<FoundAccountEventArgs> OnFoundAccount;

        void UpdateDustCoinsTo(long dustAmount);

        void Disconnect();
    }
}
