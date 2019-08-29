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
using Liviano.Interfaces;
using Liviano.Utilities;
using NBitcoin;
using Newtonsoft.Json;
using Liviano.Exceptions;
using System.Diagnostics;
using System.Collections.Generic;

namespace Liviano.Storages
{
    public class FileSystemStorage : IStorage
    {
        public string Id { get; set; }
        public Network Network { get; set; }
        public IWallet Wallet { get; set; }
        public string RootDirectory { get; set; }

        public FileSystemStorage(string id = null, string directory = "data", Network network = null)
        {
            directory = Path.GetFullPath(directory);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
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

            var filePath = GetWalletFilePath();
            var contents = File.ReadAllText(filePath);

            Wallet = JsonConvert.DeserializeObject<IWallet>(contents);

            Wallet.Accounts = LoadAccounts();
            Wallet.AccountIds = Wallet.Accounts.Select((a) => a.Id).ToList();

            // TODO: Deal with transcations saving into accounts

            return Wallet;
        }

        public void Save()
        {
            Guard.NotNull(Wallet, nameof(Wallet));
            Guard.NotNull(RootDirectory, nameof(RootDirectory));

            Id = Wallet.Id;
            Network = Wallet.Network;

            var filePath = GetWalletFilePath();
            var contents = JsonConvert.SerializeObject(Wallet);

            File.WriteAllText(filePath, contents);

            SaveAccounts();

            // TODO: Deal with transcations saving
        }

        List<IAccount> LoadAccounts()
        {
            Guard.NotNull(Wallet, nameof(Wallet));
            Guard.Assert(Wallet.Accounts is null);

            var path = GetWalletDirectory();
            var accountsPath = $"{path}{Path.DirectorySeparatorChar}accounts";
            var accounts = new List<IAccount>();

            foreach (var accountId in Wallet.AccountIds)
            {
                var fileName = $"{accountsPath}{Path.DirectorySeparatorChar}{accountId}.json";

                if (!File.Exists(fileName))
                {
                    Debug.WriteLine("FATAL! Unable to find account {fileName}");

                    continue;
                }

                var content = File.ReadAllText(fileName);
                var account = JsonConvert.DeserializeObject<IAccount>(content);

                accounts.Add(account.CastToAccountType());
            }

            return accounts;
        }

        void SaveAccounts()
        {
            var path = GetWalletDirectory();
            var accountsPath = $"{path}{Path.DirectorySeparatorChar}accounts";

            // Create "accounts" path
            if (!Directory.Exists(accountsPath))
                Directory.CreateDirectory(accountsPath);

            foreach (var account in Wallet.Accounts)
            {
                var singleAccountPath = $"{accountsPath}{Path.DirectorySeparatorChar}{account.Id}.json";
                var content = JsonConvert.SerializeObject(account);

                File.WriteAllText(singleAccountPath, content);
            }
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
    }
}
