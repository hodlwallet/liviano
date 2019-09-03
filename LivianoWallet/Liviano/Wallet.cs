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

        public List<string> TxIds { get; set; }
        public List<Tx> Txs { get; set; }

        public List<string> AccountIds { get; set; }
        public List<IAccount> Accounts { get; set; }

        IStorage _Storage;
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

        public void Init(string mnemonic, string password = "", string name = null, Network network = null, DateTimeOffset? createdAt = null, IStorage storage = null)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            Id = Id ?? Guid.NewGuid().ToString();
            Name = Name ?? name ?? DEFAULT_WALLET_NAME;

            Network = Network ?? network ?? Network.Main;

            CreatedAt = CreatedAt ?? createdAt ?? DateTimeOffset.UtcNow;

            Storage = Storage ?? storage ?? new FileSystemStorage(Id, network: Network);

            TxIds = TxIds ?? new List<string>();

            AccountIds = AccountIds ?? new List<string>();
            Accounts = Accounts ?? new List<IAccount>();

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

        public void AddTx(Tx tx)
        {
            if (TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"Wallet already has a tx with id: {tx.Id}");

                return;
            }

            TxIds.Add(tx.Id.ToString());
            Txs.Add(tx);
        }

        public void RemoveTx(Tx tx)
        {
            if (!TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"Wallet doesn't have tx with id: {tx.Id}");

                return;
            }

            Txs.Remove(tx);
            TxIds.Remove(tx.Id.ToString());
        }

        public void UpdateTx(Tx tx)
        {
            if (!TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"Wallet doesn't have tx with id: {tx.Id}");

                return;
            }

            for (int i = 0, count = Txs.Count; i < count; i++)
            {
                if (Txs[i].Id == tx.Id)
                {
                    Txs[i] = tx;
                    break;
                }
            }
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
        }

        public async Task Sync()
        {
            Debug.WriteLine($"[Sync] Attempting to sync wallet with id: {Id}");

            var electrum = await GetElectrumClient();

            using (var cts = new CancellationTokenSource())
            {
                await Task.Factory.StartNew(async () =>
                {
                    Console.WriteLine("[Sync] Syncing...");

                    var accountsWithAddresses = new Dictionary<IAccount, List<BitcoinAddress>>();
                    foreach (var account in Accounts)
                    {
                        var addresses = new List<BitcoinAddress>();

                        if (account.AccountType == "paper")
                        {
                            // Paper accounts only have one address, that's the point
                            addresses.Add(account.GetReceiveAddress());
                        }
                        else
                        {
                            // Everything else, very likely, is an HD Account.

                            // External addresses
                            var externalCount = account.ExternalAddressesCount;
                            addresses.AddRange(account.GetReceiveAddress(account.GapLimit));
                            account.ExternalAddressesCount = externalCount;

                            // Internal addresses
                            var internalCount = account.InternalAddressesCount;
                            addresses.AddRange(account.GetChangeAddress(account.GapLimit));
                            account.InternalAddressesCount = internalCount;
                        }

                        accountsWithAddresses.Add(account, addresses);
                    }

                    var tasks = new List<Task>();
                    foreach (KeyValuePair<IAccount, List<BitcoinAddress>> entry in accountsWithAddresses)
                    {
                        var t = Task.Factory.StartNew(async (obj) =>
                        {
                            var dict = (KeyValuePair<IAccount, List<BitcoinAddress>>)obj;

                            var account = dict.Key;
                            var addresses = dict.Value;

                            foreach (var addr in addresses)
                            {
                                var res = await electrum.BlockchainScriptHashGetHistory(addr.ToScriptHash().ToHex());
                            }
                        }, entry, TaskCreationOptions.LongRunning);

                        tasks.Add(t);
                    }

                    Task.WaitAll(tasks.ToArray());
                }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        public async Task Resync()
        {
            Debug.WriteLine($"[Resync] Attempting to resync wallet with id: {Id}");

            var electrum = await GetElectrumClient();
        }

        async Task<ElectrumClient> GetElectrumClient()
        {
            var recentServers = await GetRecentlyConnectedServers();

            Debug.WriteLine("[GetElectrumClient] Will connect to:");
            foreach (var server in recentServers)
            {
                Debug.WriteLine($"[GetElectrumClient] {server.Domain}:{server.PrivatePort} ({server.Version}");
            }

            return new ElectrumClient(recentServers);
        }

        async Task<List<Server>> GetRecentlyConnectedServers(bool retrying = false)
        {
            Debug.WriteLine("[GetRecentlyConnectedServers] Attempting to get the recent servers");

            var recentServers = ElectrumClient.GetRecentlyConnectedServers();

            if (recentServers.Count == 0)
            {
                Debug.WriteLine("[GetRecentlyConnectedServers] Failed to fetch connected servers. Populating list");

                // Waits 2 seconds if we need to reconnect, only on retry
                if (retrying) await Task.Delay(TimeSpan.FromSeconds(2.0));

                ElectrumClient.PopulateRecentlyConnectedServers();

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
