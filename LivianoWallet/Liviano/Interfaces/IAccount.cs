//
// IAccount.cs
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
using System.Collections.Generic;

using Newtonsoft.Json;

using NBitcoin;

using Liviano.Utilities.JsonConverters;
using Liviano.Models;
using Newtonsoft.Json.Converters;

namespace Liviano.Interfaces
{
    public interface IAccount : IHasTxs
    {
        [JsonProperty(PropertyName = "id")]
        string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }

        [JsonProperty(PropertyName = "accountType")]
        string AccountType { get; }

        [JsonProperty(PropertyName = "walletId")]
        string WalletId { get; set; }

        [JsonProperty(PropertyName = "gapLimit")]
        int GapLimit { get; set; }

        [JsonProperty(PropertyName = "colorHex")]
        string ColorHex { get; set; }

        /// <summary>
        /// Change addresses count
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "internalAddressesCount")]
        int InternalAddressesCount { get; set; }

        /// <summary>
        /// Receive addresess count
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "externalAddressesCount")]
        int ExternalAddressesCount { get; set; }

        /// <summary>
        /// Wallet the account belongs to
        /// </summary>
        /// <value></value>
        [JsonIgnore]
        IWallet Wallet { get; set; }

        /// <summary>
        /// The network this wallets belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(NetworkConverter))]
        Network Network { get; set; }

        /// <summary>
        /// Hd path, e.g. "m/0'", "m/84'/0'/0'"
        /// these will be change to full paths, with addresses, e.g:
        ///
        /// "m/84'/0'/2'/0/1 => For the 2nd, receiving (0), address of account #3
        ///                     rootPath => "m/84'/0'/2'"
        ///
        /// "m/0'/0/1        => For the 2nd, receiving (0), address of account #1 (and only)
        ///                     rootPath => "m/0'"
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "hdPath")]
        string HdPath { get; set; }

        /// <summary>
        /// An extended priv key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPubKey")]
        string ExtendedPubKey { get; set; }

        /// <summary>
        /// An extended pub key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPrivKey")]
        string ExtendedPrivKey { get; set; }

        /// <summary>
        /// The account index, corresponding to e.g.: m/{0}'
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        int Index { get; set; }

        [JsonProperty(PropertyName = "scriptPubKeyType")]
        [JsonConverter(typeof(StringEnumConverter))]
        ScriptPubKeyType ScriptPubKeyType { get; set; }

        [JsonProperty(PropertyName = "txIds", NullValueHandling = NullValueHandling.Ignore)]
        List<string> TxIds { get; set; }

        [JsonIgnore]
        List<Tx> Txs { get; set; }

        [JsonProperty(PropertyName = "usedExternalAddresses", ItemConverterType = typeof(BitcoinAddressConverter))]
        List<BitcoinAddress> UsedExternalAddresses { get; set; }

        [JsonProperty(PropertyName = "usedInternalAddresses", ItemConverterType = typeof(BitcoinAddressConverter))]
        List<BitcoinAddress> UsedInternalAddresses { get; set; }

        /// <summary>
        /// Gets 1 receiving address
        /// </summary>
        /// <returns></returns>
        BitcoinAddress GetReceiveAddress();

        /// <summary>
        /// Gets n receiving addresses
        /// </summary>
        /// <param name="n">A <see cref="int"/> of the amount of address to generate</param>
        /// <returns></returns>
        BitcoinAddress[] GetReceiveAddress(int n);

        /// <summary>
        /// Gets 1 change address
        /// </summary>
        /// <returns></returns>
        BitcoinAddress GetChangeAddress();

        /// <summary>
        /// Gets n change addresses
        /// </summary>
        /// <param name="n">A <see cref="int"/> of the amount of address to generate</param>
        /// <returns></returns>
        BitcoinAddress[] GetChangeAddress(int n);

        /// <summary>
        /// Gets the balance of the account
        /// </summary>
        /// <returns>A <see cref="Money"/> of the balance of the account</returns>
        Money GetBalance();
    }
}