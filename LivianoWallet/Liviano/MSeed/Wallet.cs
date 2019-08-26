using System;
using System.Collections.Generic;
using System.Linq;

using NBitcoin;

using Newtonsoft.Json;

using Liviano.Utilities.JsonConverters;

using Liviano.MSeed.Interfaces;
using Liviano.MSeed.Accounts;
using Liviano.Utilities;
using System.Reflection;

namespace Liviano.MSeed
{
    public class Wallet : IWallet
    {
        const string DEFAULT_WALLET_NAME = "Bitcoin Wallet";
        const string DEFAULT_ACCOUNT_NAME = "Bitcoin Account";

        Key _PrivateKey;

        ExtKey _ExtKey;

        public string[] AccountTypes => new string[] { "bip141", "paper" };

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

            var mnemonicObj = MnemonicFromString(mnemonic);
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

            switch (accountType)
            {
                case "bip141":
                    return NewBip141Account(accountName, options);
                case "paper":
                    return NewPaperAccount(accountName, options);
            }

            // This is a default account.
            return NewBip141Account(accountName, options);
        }

        PaperAccount NewPaperAccount(string accountName, object options)
        {
            var kwargs = OptionsToDict(options);

            string wif = kwargs.ContainsKey("Wif")
                ? (string)kwargs["Wif"]
                : null;

            ScriptPubKeyType scriptPubKeyType = kwargs.ContainsKey("ScriptPubKeyType")
                ? (ScriptPubKeyType)kwargs["ScriptPubKeyType"]
                : PaperAccount.DEFAULT_SCRIPT_PUB_KEY_TYPE;

            var account = new PaperAccount(
                accountName,
                scriptPubKeyType,
                wif,
                Network
            );

            return account;
        }

        Bip141Account NewBip141Account(string accountName, object options = null)
        {
            var account = new Bip141Account()
            {
                Id = NewAccountGuid(),
                Name = accountName,
                WalletId = Id,
                Wallet = this,
                Network = Network
            };

            var extPrivKey = _ExtKey.Derive(new KeyPath(account.HdRootPath));
            var extPubKey = extPrivKey.Neuter();

            account.ExtendedPrivKey = extPrivKey.ToString(Network);
            account.ExtendedPubKey = extPubKey.ToString(Network);

            return account;
        }

        string NewAccountGuid()
        {
            return Guid.NewGuid().ToString();
        }

        Mnemonic MnemonicFromString(string mnemonic)
        {
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            return new Mnemonic(mnemonic, Wordlist.AutoDetect(mnemonic));
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

        Dictionary<string, object> OptionsToDict(object options = null)
        {
            Dictionary<string, object> kwargs = new Dictionary<string, object>();

            if (options is null) return kwargs;

            foreach (PropertyInfo prop in options.GetType().GetProperties())
            {
                string propName = prop.Name;
                var val = options.GetType().GetProperty(propName).GetValue(options, null);
                if (val != null)
                {
                    kwargs.Add(propName, val);
                }
                else
                {
                    kwargs.Add(propName, null);
                }
            }

            return kwargs;
        }
    }
}
