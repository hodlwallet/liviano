using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Liviano.Utilities.JsonConverters;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace Liviano
{
    public class Wallet
    {
        /// <sumary>
        /// Default wallet constructor
        /// <sumary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="accountsRoot">List of accounts root.</param>
        /// <param name="isExtPubKeyWallet">Is the wallet an xpub or not.</param>
        /// <param name="encryptedSeed">The encrypted seed</param>
        /// <param name="chainCode">The chain code.</param>
        /// <param name="blockLocator">A block locator list.</param>
        /// <param name="network">Main, TestNet, or RegTest.</param>
        /// <param name="creationTime">Creation time, if not a default</param>

        public Wallet()
        {
            this.AccountsRoot = new List<AccountRoot>();
        }

        public Wallet(
            string name = null,
            ICollection<AccountRoot> accountsRoot = null,
            bool isExtPubKeyWallet = true,
            string encryptedSeed = null,
            byte[] chainCode = null,
            ICollection<uint256> blockLocator = null,
            Network network = null,
            DateTimeOffset creationTime = new DateTimeOffset())
       {
            this.Name = name ?? "";
            this.AccountsRoot = accountsRoot ?? new List<AccountRoot>();
            this.IsExtPubKeyWallet = isExtPubKeyWallet;
            this.EncryptedSeed = encryptedSeed ?? "";
            this.ChainCode = chainCode ?? new byte[0];
            this.BlockLocator = blockLocator ?? new List<uint256>();
            this.Network = network ?? Network.Main;
            this.CreationTime = creationTime;
        }

        /// <summary>
        /// The root of the accounts tree, a list of all the accounts belonging to this wallet.
        /// </summary>
        [JsonProperty(PropertyName = "accountsRoot")]
        public ICollection<AccountRoot> AccountsRoot { get; set; }

        /// <summary>
        /// The name of this wallet.
        /// </summary>
        public Wallet(string name, bool isExtPubKeyWallet, string encryptedSeed, Network network, DateTimeOffset creationTime)
        {
            this.Name = name;
            this.IsExtPubKeyWallet = isExtPubKeyWallet;
            this.EncryptedSeed = encryptedSeed;
            this.Network = network;
            this.CreationTime = creationTime;
        }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Flag indicating if it is a watch only wallet.
        /// </summary>
        [JsonProperty(PropertyName = "isExtPubKeyWallet")]
        public bool IsExtPubKeyWallet { get; set; }

        /// <summary>
        /// The seed for this wallet, password encrypted.
        /// </summary>
        [JsonProperty(PropertyName = "encryptedSeed", NullValueHandling = NullValueHandling.Ignore)]
        public string EncryptedSeed { get; set; }

        /// <summary>
        /// The chain code.
        /// </summary>
        [JsonProperty(PropertyName = "chainCode", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] ChainCode { get; set; }

        /// <summary>
        /// Gets or sets the merkle path, locator.
        /// </summary>
        [JsonProperty(PropertyName = "blockLocator", ItemConverterType = typeof(Utilities.JsonConverters.UInt256JsonConverter))]
        public ICollection<uint256> BlockLocator { get; set; }

        /// <summary>
        /// The network this wallets belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        public Network Network { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(Utilities.JsonConverters.DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }


        /// <summary>
        /// Gets the accounts the wallet has for this type of coin.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns>The accounts in the wallet corresponding to this type of coin.</returns>
        public IEnumerable<HdAccount> GetAccountsByCoinType(CoinType coinType)
        {
            return this.AccountsRoot.Where(a => a.CoinType == coinType).SelectMany(a => a.Accounts);
        }

        /// <summary>
        /// Gets an account from the wallet's accounts.
        /// </summary>
        /// <param name="accountName">The name of the account to retrieve.</param>
        /// <param name="coinType">The type of the coin this account is for.</param>
        /// <returns>The requested account.</returns>
        public HdAccount GetAccountByCoinType(string accountName, CoinType coinType)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);
            return accountRoot?.GetAccountByName(accountName);
        }

        /// <summary>
        /// Update the last block synced height and hash in the wallet.
        /// </summary>
        /// <param name="coinType">The type of the coin this account is for.</param>
        /// <param name="block">The block whose details are used to update the wallet.</param>
        public void SetLastBlockDetailsByCoinType(CoinType coinType, ChainedBlock block)
        {
            AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);

            if (accountRoot == null) return;

            accountRoot.LastBlockSyncedHeight = block.Height;
            accountRoot.LastBlockSyncedHash = block.HashBlock;
        }

        /// <summary>
        /// Gets all the transactions by coin type.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetAllTransactionsByCoinType(CoinType coinType)
        {
            List<HdAccount> accounts = this.GetAccountsByCoinType(coinType).ToList();

            foreach (TransactionData txData in accounts.SelectMany(x => x.ExternalAddresses).SelectMany(x => x.Transactions))
            {
                yield return txData;
            }

            foreach (TransactionData txData in accounts.SelectMany(x => x.InternalAddresses).SelectMany(x => x.Transactions))
            {
                yield return txData;
            }
        }

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<Script> GetAllPubKeysByCoinType(CoinType coinType)
        {
            List<HdAccount> accounts = this.GetAccountsByCoinType(coinType).ToList();

            foreach (Script script in accounts.SelectMany(x => x.ExternalAddresses).Select(x => x.ScriptPubKey))
            {
                yield return script;
            }

            foreach (Script script in accounts.SelectMany(x => x.InternalAddresses).Select(x => x.ScriptPubKey))
            {
                yield return script;
            }
        }

        /// <summary>
        /// Gets all the addresses contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns>A list of all the addresses contained in this wallet.</returns>
        public IEnumerable<HdAddress> GetAllAddressesByCoinType(CoinType coinType)
        {
            List<HdAccount> accounts = this.GetAccountsByCoinType(coinType).ToList();

            var allAddresses = new List<HdAddress>();
            foreach (HdAccount account in accounts)
            {
                allAddresses.AddRange(account.GetCombinedAddresses());
            }
            return allAddresses;
        }

        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="password">The password used to decrypt the wallet's <see cref="EncryptedSeed"/>.</param>
        /// <param name="coinType">The type of coin this account is for.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, CoinType coinType, DateTimeOffset accountCreationTime)
        {
            Guard.NotEmpty(password, nameof(password));

            AccountRoot accountRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);
            return accountRoot.AddNewAccount(password, this.EncryptedSeed, this.ChainCode, this.Network, accountCreationTime);
        }

        // <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="extPubKey">The extended public key for the wallet<see cref="EncryptedSeed"/>.</param>
        /// <param name="coinType">The type of coin this account is for.</param>
        /// <param name="accountIndex">Zero-based index of the account to add.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(CoinType coinType, ExtPubKey extPubKey, int accountIndex, DateTimeOffset accountCreationTime)
        {
            AccountRoot accountRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);
            return accountRoot.AddNewAccount(extPubKey, accountIndex, this.Network, accountCreationTime);
        }


        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account.</returns>
        public HdAccount GetFirstUnusedAccount(CoinType coinType)
        {
            // Get the accounts root for this type of coin.
            AccountRoot accountsRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);

            if (accountsRoot.Accounts.Any())
            {
                // Get an unused account.
                HdAccount firstUnusedAccount = accountsRoot.GetFirstUnusedAccount();
                if (firstUnusedAccount != null)
                {
                    return firstUnusedAccount;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether the wallet contains the specified address.
        /// </summary>
        /// <param name="address">The address to check.</param>
        /// <returns>A value indicating whether the wallet contains the specified address.</returns>
        public bool ContainsAddress(HdAddress address)
        {
            if (!this.AccountsRoot.Any(r => r.Accounts.Any(
                a => a.ExternalAddresses.Any(i => i.Address == address.Address) ||
                     a.InternalAddresses.Any(i => i.Address == address.Address))))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        public ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(address, nameof(address));

            // Check if the wallet contains the address.
            if (!this.ContainsAddress(address))
            {
                throw new Exception("Address not found on wallet.");
            }

            // get extended private key
            Key privateKey = HdOperations.DecryptSeed(this.EncryptedSeed, password, this.Network);
            return HdOperations.GetExtendedPrivateKey(privateKey, this.ChainCode, address.HdPath, this.Network);
        }

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin to get transactions from.</param>
        /// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
        /// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
        /// <returns>A collection of spendable outputs.</returns>
        public IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(CoinType coinType, int currentChainHeight, int confirmations = 0)
        {
            IEnumerable<HdAccount> accounts = this.GetAccountsByCoinType(coinType);

            return accounts
                .SelectMany(x => x.GetSpendableTransactions(currentChainHeight, confirmations));
        }
    }


    public enum CoinType
    {
        Bitcoin = 0
    }
}
