using Liviano.Utilities.JsonConverters;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

using Liviano.Utilities;

namespace Liviano.Models
{
    public class AccountRoot
    {
        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        public AccountRoot()
        {
            this.Accounts = new List<HdAccount>();
        }

        /// <summary>
        /// The type of coin, Bitcoin or Stratis.
        /// </summary>
        [JsonProperty(PropertyName = "coinType")]
        public CoinType CoinType { get; set; }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? LastBlockSyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 LastBlockSyncedHash { get; set; }

        /// <summary>
        /// The accounts used in the wallet.
        /// </summary>
        [JsonProperty(PropertyName = "accounts")]
        public ICollection<HdAccount> Accounts { get; set; }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account</returns>
        public HdAccount GetFirstUnusedAccount()
        {
            if (this.Accounts == null)
                return null;

            List<HdAccount> unusedAccounts = this.Accounts.Where(acc => !acc.ExternalAddresses.Any() && !acc.InternalAddresses.Any()).ToList();
            if (!unusedAccounts.Any())
                return null;

            // gets the unused account with the lowest index
            int index = unusedAccounts.Min(a => a.Index);
            return unusedAccounts.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the account matching the name passed as a parameter.
        /// </summary>
        /// <param name="accountName">The name of the account to get.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public HdAccount GetAccountByName(string accountName)
        {
            if (this.Accounts == null)
                throw new WalletException($"No account with the name {accountName} could be found.");

            // get the account
            HdAccount account = this.Accounts.SingleOrDefault(a => a.Name == accountName);
            if (account == null)
                throw new WalletException($"No account with the name {accountName} could be found.");

            return account;
        }

        /// <summary>
        /// Adds an account to the current account root.
        /// </summary>
        /// <remarks>The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains transactions.
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/></remarks>
        /// <param name="password">The password used to decrypt the wallet's encrypted seed.</param>
        /// <param name="encryptedSeed">The encrypted private key for this wallet.</param>
        /// <param name="chainCode">The chain code for this wallet.</param>
        /// <param name="network">The network for which this account will be created.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Get the current collection of accounts.
            List<HdAccount> accounts = this.Accounts.ToList();

            int newAccountIndex = 0;
            if (accounts.Any())
            {
                newAccountIndex = accounts.Max(a => a.Index) + 1;
            }

            // Get the extended pub key used to generate addresses for this account.
            string accountHdPath = HdOperations.GetAccountHdPath((int)this.CoinType, newAccountIndex);
            Key privateKey = HdOperations.DecryptSeed(encryptedSeed, password, network);
            ExtPubKey accountExtPubKey = HdOperations.GetExtendedPublicKey(privateKey, chainCode, accountHdPath);

            var newAccount = new HdAccount
            {
                Index = newAccountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {newAccountIndex}",
                HdPath = accountHdPath,
                CreationTime = accountCreationTime
            };

            accounts.Add(newAccount);
            this.Accounts = accounts;

            return newAccount;
        }

        /// <inheritdoc cref="AddNewAccount(string, string, byte[], Network, DateTimeOffset)"/>
        /// <summary>
        /// Adds an account to the current account root using extended public key and account index.
        /// </summary>
        /// <param name="accountExtPubKey">The extended public key for the account.</param>
        /// <param name="accountIndex">The zero-based account index.</param>
        public HdAccount AddNewAccount(ExtPubKey accountExtPubKey, int accountIndex, Network network, DateTimeOffset accountCreationTime)
        {
            ICollection<HdAccount> hdAccounts = this.Accounts.ToList();

            if (hdAccounts.Any(a => a.Index == accountIndex))
            {
                throw new WalletException("There is already an account in this wallet with index: " + accountIndex);
            }

            if (hdAccounts.Any(x => x.ExtendedPubKey == accountExtPubKey.ToString(network)))
            {
                throw new WalletException("There is already an account in this wallet with this xpubkey: " +
                                            accountExtPubKey.ToString(network));
            }

            string accountHdPath = HdOperations.GetAccountHdPath((int) this.CoinType, accountIndex);

            var newAccount = new HdAccount
            {
                Index = accountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {accountIndex}",
                HdPath = accountHdPath,
                CreationTime = accountCreationTime
            };

            hdAccounts.Add(newAccount);
            this.Accounts = hdAccounts;

            return newAccount;
        }
    }
}
