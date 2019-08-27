//
// PaperAccount.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 
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
using System.Linq;

using NBitcoin;

using Liviano.MSeed.Interfaces;
using Liviano.MSeed.Accounts;
using Liviano.Utilities;
using Liviano.Extensions;

namespace Liviano.MSeed
{
    public class Wallet : IWallet
    {
        const string DEFAULT_WALLET_NAME = "Bitcoin Wallet";
        const string DEFAULT_ACCOUNT_NAME = "Bitcoin Account";

        Key _PrivateKey;

        ExtKey _ExtKey;

        public string[] AccountTypes => new string[] { "bip141", "bip44", "bip49", "bip84", "paper" };

        public string Id { get; set; }

        public string Name { get; set; }

        public Network Network { get; set; }

        public string EncryptedSeed { get; set; }

        public byte[] ChainCode { get; set; }

        public List<string> TxIds { get; set; }

        public List<Dictionary<string, string>> AccountIds { get; set; }

        public List<IAccount> Accounts { get; set; }

        public Wallet()
        {
        }

        public void Init(string mnemonic, string password = "", string name = null, Network network = null)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            Id = Id ?? Guid.NewGuid().ToString();
            Name = Name ?? name ?? DEFAULT_WALLET_NAME;

            Network = Network ?? network ?? Network.Main;

            TxIds = TxIds ?? new List<string>();

            AccountIds = AccountIds ?? new List<Dictionary<string, string>>();
            Accounts = Accounts ?? new List<IAccount>();

            var mnemonicObj = HdOperations.MnemonicFromString(mnemonic);
            var extKey = HdOperations.GetExtendedKey(mnemonicObj, password);

            EncryptedSeed = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, Network).ToWif();
            ChainCode = extKey.ChainCode;

            _ = GetPrivateKey(password);
            _ = GetExtendedKey(password);
        }

        public Key GetPrivateKey(string password = "", bool forcePasswordVerification = false)
        {
            if (_PrivateKey == null || forcePasswordVerification)
                _PrivateKey = HdOperations.DecryptSeed(EncryptedSeed, Network, password);

            return _PrivateKey;
        }

        public void AddAccount(string accountType = "", string accountName = null, object options = null)
        {
            var account = NewAccount(accountType, accountName, options);

            Accounts.Add(account);
        }

        IAccount NewAccount(string accountType = "", string accountName = null, object options = null)
        {
            Guard.NotEmpty(accountType, nameof(accountType));

            if (!AccountTypes.Contains(accountType))
                throw new ArgumentException($"Invalid account type: {accountType}");

            // TODO get this from the wallet I guess we need translations in Liviano as well
            if (string.IsNullOrEmpty(accountName) && Accounts.Count == 0)
                accountName = DEFAULT_ACCOUNT_NAME;

            if (string.IsNullOrEmpty(accountName))
                throw new ArgumentException("Invalid account name: It cannot be empty!");

            return CreateAccount(accountType, accountName, options);
        }

        IAccount CreateAccount(string type, string name, object options)
        {
            switch (type)
            {
                case "bip44":
                case "bip49":
                case "bip84":
                case "bip141":
                    return CreateBip32Account(type, name, options);
                case "paper":
                    return CreatePaperAccount(name, options);
            }

            return CreateBip32Account(name, name, options);
        }

        PaperAccount CreatePaperAccount(string name, object options)
        {
            var kwargs = options.ToDict();

            string wif = kwargs.ContainsKey("Wif")
                ? (string)kwargs["Wif"]
                : null;

            ScriptPubKeyType scriptPubKeyType = kwargs.ContainsKey("ScriptPubKeyType")
                ? (ScriptPubKeyType)kwargs["ScriptPubKeyType"]
                : PaperAccount.DEFAULT_SCRIPT_PUB_KEY_TYPE;

            var account = new PaperAccount(
                name,
                scriptPubKeyType,
                wif,
                Network
            );

            return account;
        }

        Bip32Account CreateBip32Account(string type, string name, object options = null)
        {
            Bip32Account account = null;
            switch (type)
            {
                case "bip44":
                    account = new Bip44Account();
                    break;
                case "bip49":
                    account = new Bip49Account();
                    break;
                case "bip84":
                    account = new Bip84Account();
                    break;
                case "bip141":
                    account = new Bip141Account();
                    break;
            }

            if (account is null)
                throw new ArgumentException($"Incorrect account type: {type}");

            account.Name = name;
            account.WalletId = Id;
            account.Wallet = this;
            account.Network = Network;

            var extPrivKey = _ExtKey.Derive(new KeyPath(account.HdPath));
            var extPubKey = extPrivKey.Neuter();

            account.ExtendedPrivKey = extPrivKey.ToString(Network);
            account.ExtendedPubKey = extPubKey.ToString(Network);

            return account;
        }

        ExtKey GetExtendedKey(string password = "", bool forcePasswordVerification = false)
        {
            Guard.NotNull(_PrivateKey, nameof(_PrivateKey));
            Guard.NotNull(ChainCode, nameof(ChainCode));

            if (forcePasswordVerification)
                _PrivateKey = GetPrivateKey(password, forcePasswordVerification);

            if (_ExtKey is null || forcePasswordVerification)
                _ExtKey = new ExtKey(_PrivateKey, ChainCode);

            return _ExtKey;
        }
    }
}
