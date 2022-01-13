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
using System;
using System.Collections.Generic;
using System.ComponentModel;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using NBitcoin;

using Liviano.Models;

namespace Liviano.Interfaces
{
    public interface IAccount : IHasTxs, ICloneable
    {
        /// <summary>
        /// Guid of the account
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        string Id { get; set; }

        /// <summary>
        /// Name of the account to be shown in the user dashboard potentially
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }

        /// <summary>
        /// Account type as a string, bip32, bip44, bip84, bip141
        /// all are possible values and are defined by concrete classes
        /// </summary>
        [JsonProperty(PropertyName = "accountType")]
        string AccountType { get; }

        /// <summary>
        /// An account belongs to a wallet, this is its id
        /// </summary>
        [JsonProperty(PropertyName = "walletId")]
        string WalletId { get; set; }

        /// <summary>
        /// Gap limit of unused addresses to pick addresses from
        /// </summary>
        [JsonProperty(PropertyName = "gapLimit")]
        int GapLimit { get; set; }

        /// <summary>
        /// Where in the gap is the user currently,
        /// if we have used address lindex 10 and the gap is 30,
        /// and the user requested 3 accounts then this will be 2 (0 indexed)
        /// Index in the gap the user requested external addresses
        /// </summary>
        [JsonProperty(PropertyName = "externalAddressesGapIndex")]
        int ExternalAddressesGapIndex { get; set; }

        /// <summary>
        /// Index in the gap the user requested internal addresses
        /// </summary>
        [JsonProperty(PropertyName = "internalAddressesGapIndex")]
        int InternalAddressesGapIndex { get; set; }

        /// <summary>
        /// Change addresses index, last internal address used index
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "internalAddressesIndex")]
        int InternalAddressesIndex { get; set; }

        /// <summary>
        /// Receive addresses index, last external address used index
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "externalAddressesIndex")]
        int ExternalAddressesIndex { get; set; }

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
        BitcoinExtPubKey ExtPubKey { get; set; }

        /// <summary>
        /// An extended pub key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPrivKey")]
        BitcoinExtKey ExtKey { get; set; }

        /// <summary>
        /// The account index, corresponding to e.g.: m/{0}'
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        int Index { get; set; }

        /// <summary>
        /// ScriptPubKeyTypes of the addresses that it generates
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKeyTypes", ItemConverterType = typeof(StringEnumConverter))]
        List<ScriptPubKeyType> ScriptPubKeyTypes { get; set; }

        /// <summary>
        /// Transactions ids sent to this account
        /// </summary>
        [JsonProperty(PropertyName = "txIds", NullValueHandling = NullValueHandling.Ignore)]
        List<string> TxIds { get; set; }

        /// <summary>
        /// The transactions sent to this account
        /// </summary>
        [JsonIgnore]
        List<Tx> Txs { get; set; }

        /// <summary>
        /// The UTXO list from the account
        /// </summary>
        [JsonProperty(PropertyName = "unspentCoins", NullValueHandling = NullValueHandling.Ignore)]
        List<Coin> UnspentCoins { get; set; }

        /// <summary>
        /// The spent transaction outputs
        /// </summary>
        [JsonProperty(PropertyName = "spentCoins", NullValueHandling = NullValueHandling.Ignore)]
        List<Coin> SpentCoins { get; set; }

        /// <summary>
        /// The frozen (banned or blocked) transaction outputs
        /// to prevent usage during coin selection, to use in coin control features
        /// </summary>
        [JsonProperty(PropertyName = "frozenCoins", NullValueHandling = NullValueHandling.Ignore)]
        List<Coin> FrozenCoins { get; set; }

        /// <summary>
        /// Dust prevention minimum value, more than equal this amount is okay less is not
        /// </summary>
        [JsonProperty(PropertyName = "dustMinValue", NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(0)]
        long DustMinValue { get; set; }

        /// <summary>
        /// All external addresses generated
        /// </summary>
        [JsonProperty(PropertyName = "externalAddresses", NullValueHandling = NullValueHandling.Ignore)]
        Dictionary<ScriptPubKeyType, List<BitcoinAddressWithMetadata>> ExternalAddresses { get; set; }

        /// <summary>
        /// All internal addresses generated
        /// </summary>
        [JsonProperty(PropertyName = "internalAddresses", NullValueHandling = NullValueHandling.Ignore)]
        Dictionary<ScriptPubKeyType, List<BitcoinAddressWithMetadata>> InternalAddresses { get; set; }

        /// <summary>
        /// List of used external addresses
        /// </summary>
        [JsonProperty(PropertyName = "usedExternalAddresses", NullValueHandling = NullValueHandling.Ignore)]
        List<BitcoinAddress> UsedExternalAddresses { get; set; }

        /// <summary>
        /// List of used internal addresses
        /// </summary>
        [JsonProperty(PropertyName = "usedInternalAddresses", NullValueHandling = NullValueHandling.Ignore)]
        List<BitcoinAddress> UsedInternalAddresses { get; set; }

        /// <summary>
        /// Add UTXO
        /// </summary>
        void AddUtxo(Coin coin);

        /// <summary>
        /// Remove UTXO
        /// </summary>
        void RemoveUtxo(Coin coin);

        /// <summary>
        /// Freeze UTXO
        /// </summary>
        void FreezeUtxo(Coin coin);

        /// <summary>
        /// Unfreeze UTXO
        /// </summary>
        void UnfreezeUtxo(Coin coin);

        /// <summary>
        /// Delete UTXO permantently (an rbf utxo)
        /// </summary>
        void DeleteUtxo(Coin coin);

        /// <summary>
        /// Update UTXO list with the data from a transaction
        /// </summary>
        void UpdateUtxoListWithTransaction(Transaction transaction);

        /// <summary>
        /// Finds and remove duplicate utxos on account
        /// </summary>
        void FindAndRemoveDuplicateUtxo();

        /// <summary>
        /// Gets 1 receiving address
        /// </summary>
        /// <param name="typeIndex">A index of the scriptPubKeyTypes</param>
        /// <returns>A <see cref="BitcoinAddress"/></returns>
        BitcoinAddress GetReceiveAddress(int typeIndex = 0);

        /// <summary>
        /// Gets n receiving addresses
        /// </summary>
        /// <param name="n">A <see cref="int"/> of the amount of address to generate</param>
        /// <param name="typeIndex">A index of the scriptPubKeyTypes</param>
        /// <returns>An <see cref="Array"/> of <see cref="BitcoinAddresses"/> specificed by <see cref="int">n</see></returns>
        BitcoinAddress[] GetReceiveAddress(int n, int typeIndex = 0);

        /// <summary>
        /// Gets 1 change address
        /// </summary>
        /// <param name="typeIndex">A index of the scriptPubKeyTypes</param>
        /// <returns>A <see cref="BitcoinAddress"/></returns>
        BitcoinAddress GetChangeAddress(int typeIndex = 0);

        /// <summary>
        /// Gets n change addresses
        /// </summary>
        /// <param name="n">A <see cref="int"/> of the amount of address to generate</param>
        /// <param name="typeIndex">A index of the scriptPubKeyTypes</param>
        /// <returns>An <see cref="Array"/> of <see cref="BitcoinAddresses"/> specificed by <see cref="int">n</see></returns>
        BitcoinAddress[] GetChangeAddress(int n, int typeIndex = 0);

        /// <summary>
        /// Gets a receive address at an index without increasing the count
        /// </summary>
        /// <param name="i">A <see cref="int"/> index of the address to get</param>
        /// <param name="typeIndex">A index of the scriptPubKeyTypes</param>
        /// <returns>A <see cref="BitcoinAddress"/></returns>
        BitcoinAddress GetReceiveAddressAtIndex(int i, int typeIndex = 0);

        /// <summary>
        /// Gets a change address at an index without increasing the count
        /// </summary>
        /// <param name="i">A <see cref="int"/> index of the address to get</param>
        /// <param name="typeIndex">A index of the scriptPubKeyTypes</param>
        /// <returns>A <see cref="BitcoinAddress"/></returns>
        BitcoinAddress GetChangeAddressAtIndex(int i, int typeIndex = 0);

        /// <summary>
        /// Gets all receive addresses to watch, start on 0 and get all until grap
        /// </summary>
        BitcoinAddress[] GetReceiveAddressesToWatch();

        /// <summary>
        /// Gets all change addresses to watch, start on 0 and get all until grap
        /// </summary>
        BitcoinAddress[] GetChangeAddressesToWatch();

        /// <summary>
        /// Gets all addresses to watch, first receive then change
        /// </summary>
        BitcoinAddress[] GetAddressesToWatch();

        /// <summary>
        /// Gets external address index
        /// </summary>
        int GetExternalIndex(BitcoinAddress address);

        /// <summary>
        /// Gets internal address index
        /// </summary>
        int GetInternalIndex(BitcoinAddress address);

        /// <summary>
        /// Gets the last external index
        /// </summary>
        int GetExternalLastIndex();

        /// <summary>
        /// Gets the last external index
        /// </summary>
        int GetInternalLastIndex();

        /// <summary>
        /// Generates new addresses until filling up the gap
        /// </summary>
        void GenerateNewAddresses();

        /// <summary>
        /// Gets the balance of the account
        /// </summary>
        /// <returns>A <see cref="Money"/> of the balance of the account</returns>
        Money GetBalance();

        /// <summary>
        /// True if the address is in ExternalAddresses on any scriptpubkey
        /// </summary>
        bool IsReceive(BitcoinAddress address);

        /// <summary>
        /// True if the address is in InternalAddresses on any scriptpubkey
        /// </summary>
        bool IsChange(BitcoinAddress address);

        /// <summary>
        /// Update Dust coins
        /// </summary>
        void UpdateDustCoins();
    }
}
