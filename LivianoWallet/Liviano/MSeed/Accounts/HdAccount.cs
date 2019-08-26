using System;
using System.Collections.Generic;

using NBitcoin;

using Newtonsoft.Json;

using Liviano.MSeed;
using Liviano.MSeed.Interfaces;

namespace Liviano.MSeed.Accounts
{
    public abstract class HdAccount : IAccount
    {
        public const int GAP_LIMIT = 20;

        public string Id { get; set; }

        public abstract string AccountType { get; set; }

        public string WalletId { get; set; }

        /// <summary>
        /// Change addresses count
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "internalAddressesCount")]
        public int InternalAddressesCount { get; set; }

        /// <summary>
        /// Receive addresess count
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "externalAddressesCount")]
        public int ExternalAddressesCount { get; set; }

        /// <summary>
        /// Wallet the account belongs to
        /// </summary>
        /// <value></value>
        public Wallet Wallet { get; set; }

        /// <summary>
        /// Hd root path, e.g. "m/0'", "m/84'/0'/0'"
        /// these will be change to full paths, e.g:
        ///
        /// "m/84'/0'/2'/0/1 => For the 2nd address of account #3
        ///                     rootPath => "m/84'/0'/2'"
        ///
        /// "m/0'/0/1        => For the 2nd address of account #1 (and only)
        ///                     rootPath => "m/0'"
        /// </summary>
        /// <value></value>
        public string HdRootPath { get; set; }

        public Network Network { get; set; }

        public string Name { get; set; }

        public List<string> TxIds { get; set; }

        /// <summary>
        /// An extended priv key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPubKey")]
        public string ExtendedPubKey { get; set; }

        /// <summary>
        /// An extended pub key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPrivKey")]
        public string ExtendedPrivKey { get; set; }

        public abstract BitcoinAddress GetReceiveAddress();

        public abstract BitcoinAddress[] GetReceiveAddress(int n);

        public abstract BitcoinAddress GetChangeAddress();

        public abstract BitcoinAddress[] GetChangeAddress(int n);
    }
}