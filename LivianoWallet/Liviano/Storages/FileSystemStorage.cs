//
// FileSystemStorage.cs
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
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

using NBitcoin;

using Newtonsoft.Json;

using Liviano.Interfaces;
using Liviano.Utilities;
using Liviano.Models;
using Liviano.Accounts;

namespace Liviano.Storages
{
    public class FileSystemStorage : IStorage
    {
        public string Id { get; set; }
        public Network Network { get; set; }
        public IWallet Wallet { get; set; }
        public string RootDirectory { get; set; }

        public FileSystemStorage(string id = null, Network network = null, string directory = "wallets")
        {
            directory = Path.GetFullPath(directory);
            if (!Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            Id = id;
            RootDirectory = directory;
            Network = network;
        }

        public IWallet Load()
        {
            Guard.NotNull(Id, nameof(Id));
            Guard.NotNull(Network, nameof(Network));
            Guard.Assert(Wallet is null);
            Guard.Assert(Exists());

            var filePath = GetWalletFilePath();
            var contents = File.ReadAllText(filePath);

            Wallet = JsonConvert.DeserializeObject<Wallet>(contents);

            Wallet.Accounts = GetAccounts();
            Wallet.AccountIds = Wallet.Accounts.Select((a) => a.Id).ToList();

            foreach (var account in Wallet.Accounts)
            {
                account.Txs = GetTxs(account);
                account.TxIds = account.Txs.Select((tx) => tx.Id.ToString()).ToList();
            }

            if (string.IsNullOrEmpty(Wallet.CurrentAccountId) && Wallet.Accounts.Count > 0)
            {
                Wallet.CurrentAccount = Wallet.Accounts[0];
            }

            Wallet.Storage = this;

            return Wallet;
        }

        public void Save()
        {
            Guard.NotNull(Wallet, nameof(Wallet));
            Guard.NotNull(RootDirectory, nameof(RootDirectory));

            Id = Wallet.Id;
            Network = Wallet.Network;

            var filePath = GetWalletFilePath();
            var contents = JsonConvert.SerializeObject(Wallet, Formatting.Indented);

            File.WriteAllText(filePath, contents);

            SaveAccounts();
            SaveTxs();
        }

        List<Tx> GetTxs(IAccount account)
        {
            Guard.NotNull(Wallet, nameof(Wallet));

            var txsPath = GetTxsPath();
            var txs = new List<Tx>();

            foreach (var txId in account.TxIds)
            {
                var txFilePath = $"{txsPath}{Path.DirectorySeparatorChar}{txId}.json";

                var contents = File.ReadAllText(txFilePath);
                var tx = JsonConvert.DeserializeObject<Tx>(contents);

                txs.Add(tx);
            }

            return txs;
        }

        List<IAccount> GetAccounts()
        {
            Guard.NotNull(Wallet, nameof(Wallet));
            Guard.Assert(Wallet.Accounts is null);

            var accountsPath = GetAccountsPath();
            var accounts = new List<IAccount>();

            foreach (var accountId in Wallet.AccountIds)
            {
                var fileName = $"{accountsPath}{Path.DirectorySeparatorChar}{accountId}.json";

                if (!File.Exists(fileName))
                {
                    Debug.WriteLine($"FATAL! Unable to find account: {fileName}");

                    continue;
                }

                var content = File.ReadAllText(fileName);

                var accountType = JsonConvert.DeserializeAnonymousType(
                    content, new { accountType = " " }
                    ).accountType;

                switch (accountType)
                {
                    case "bip32":
                        accounts.Add(JsonConvert.DeserializeObject<Bip32Account>(content));
                        break;
                    case "bip44":
                        accounts.Add(JsonConvert.DeserializeObject<Bip44Account>(content));
                        break;
                    case "bip49":
                        accounts.Add(JsonConvert.DeserializeObject<Bip49Account>(content));
                        break;
                    case "bip84":
                        accounts.Add(JsonConvert.DeserializeObject<Bip84Account>(content));
                        break;
                    case "bip141":
                        accounts.Add(JsonConvert.DeserializeObject<Bip141Account>(content));
                        break;
                    case "wasabi":
                        accounts.Add(JsonConvert.DeserializeObject<WasabiAccount>(content));
                        break;
                    case "paper":
                        accounts.Add(JsonConvert.DeserializeObject<PaperAccount>(content));
                        break;
                }

                var recentAccount = accounts.Last();
                if (recentAccount.StartHex == null || recentAccount.EndHex == null)
                {
                    var colors = Liviano.Wallet.GradientHex();
                    recentAccount.StartHex = colors.Item1;
                    recentAccount.EndHex = colors.Item2;
                }
            }

            return accounts;
        }

        void SaveAccounts()
        {
            var accountsPath = GetAccountsPath();

            // Create "accounts" path
            if (!Directory.Exists(accountsPath))
                Directory.CreateDirectory(accountsPath);

            if (Wallet.Accounts is null) return;

            foreach (var account in Wallet.Accounts)
            {
                var singleAccountPath = $"{accountsPath}{Path.DirectorySeparatorChar}{account.Id}.json";
                var content = JsonConvert.SerializeObject(account, Formatting.Indented);

                File.WriteAllText(singleAccountPath, content);
            }
        }

        void SaveTxs()
        {
            var txsPath = GetTxsPath();

            foreach (var account in Wallet.Accounts)
            {
                if (account.Txs is null) continue;

                foreach (var tx in account.Txs)
                {
                    var filePath = $"{txsPath}{Path.DirectorySeparatorChar}{tx.Id}.json";
                    var contents = JsonConvert.SerializeObject(tx, Formatting.Indented);

                    File.WriteAllText(filePath, contents);
                }
            }
        }

        string GetAccountsPath()
        {
            var path = GetWalletDirectory();
            var accountsPath = $"{path}{Path.DirectorySeparatorChar}accounts";

            if (!Directory.Exists(accountsPath))
                Directory.CreateDirectory(accountsPath);

            return accountsPath;
        }

        string GetTxsPath()
        {
            var path = GetWalletDirectory();
            var transactionsPath = $"{path}{Path.DirectorySeparatorChar}transactions";

            if (!Directory.Exists(transactionsPath))
                Directory.CreateDirectory(transactionsPath);

            return transactionsPath;
        }

        string GetWalletDirectory()
        {
            // e.g.: "/home/igor/.liviano"
            var path = RootDirectory;

            // "\" or "/"
            path += Path.DirectorySeparatorChar;

            // "main" or "testnet"
            path += Network.Name.ToLower();

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            path += Path.DirectorySeparatorChar;

            // "de88e127-8c41-4b70-a226-de38189b38b1"
            path += Id;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        string GetWalletFilePath()
        {
            // e.g.: "/home/igor/.liviano/testnet"
            var path = GetWalletDirectory();

            // "\" or "/"
            path += Path.DirectorySeparatorChar;

            // e.g.: "/home/igor/.liviano/testnet/de88e127-8c41-4b70-a226-de38189b38b1/wallet.json"
            path += "wallet.json";

            return path;
        }

        public bool Exists()
        {
            Guard.NotNull(Id, nameof(Id));

            return File.Exists(GetWalletFilePath());
        }

        public void Delete()
        {
            Guard.NotNull(Wallet, nameof(Wallet));
            Guard.NotNull(RootDirectory, nameof(RootDirectory));

            var path = GetWalletDirectory();

            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
