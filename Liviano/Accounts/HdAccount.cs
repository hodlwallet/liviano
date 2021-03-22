//
// HdAccount.cs
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
using System.Collections.Generic;

using NBitcoin;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Liviano.Interfaces;
using Liviano.Models;
using Liviano.Exceptions;
using Liviano.Events;

namespace Liviano.Accounts
{
    public abstract class HdAccount : IAccount
    {
        /// <summary>
        /// Id of the account, usually a guid
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Account type, a string e.g.: "bip141"
        /// </summary>
        public abstract string AccountType { get; }

        /// <summary>
        /// The id of the wallet that this belongs to
        /// </summary>
        public string WalletId { get; set; }

        /// <summary>
        /// This is the amount of address to generate
        /// </summary>
        int gapLimit = 20;
        public int GapLimit
        {
            get => gapLimit;
            set
            {
                if (value < 0) throw new ArgumentException($"Invalid value {value}");

                gapLimit = value;
            }
        }

        /// <summary>
        /// Change addresses count
        /// </summary>
        /// <value></value>
        int internalAddressesCount = 0;
        [JsonProperty(PropertyName = "internalAddressesCount")]
        public int InternalAddressesCount
        {
            get => internalAddressesCount;
            set => internalAddressesCount = value >= GapLimit + InternalAddressesIndex ? InternalAddressesIndex : value;
        }

        /// <summary>
        /// Change addresess index
        /// </summary>
        /// <value></value>
        int internalAddressesIndex = 0;
        [JsonProperty(PropertyName = "internalAddressesIndex")]
        public int InternalAddressesIndex
        {
            get => internalAddressesIndex;
            set => internalAddressesIndex = value;
        }

        /// <summary>
        /// Receive addresess count
        /// </summary>
        /// <value></value>
        int externalAddressesCount = 0;
        [JsonProperty(PropertyName = "externalAddressesCount")]
        public int ExternalAddressesCount
        {
            get => externalAddressesCount;
            set => externalAddressesCount = value >= GapLimit + ExternalAddressesIndex ? ExternalAddressesIndex : value;
        }

        /// <summary>
        /// Receive addresess index
        /// </summary>
        /// <value></value>
        int externalAddressesIndex = 0;
        [JsonProperty(PropertyName = "externalAddressesIndex")]
        public int ExternalAddressesIndex
        {
            get => externalAddressesIndex;
            set => externalAddressesIndex = value;
        }

        /// <summary>
        /// Wallet the account belongs to
        /// </summary>
        /// <value></value>
        [JsonIgnore]
        public IWallet Wallet { get; set; }

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
        public string HdPath { get; set; }

        /// <summary>
        /// An extended priv key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPubKey")]
        public BitcoinExtPubKey ExtPubKey { get; set; }

        /// <summary>
        /// An extended private key used to generate addresses.
        /// </summary>
        [JsonIgnore]
        public BitcoinExtKey ExtKey { get; set; }

        /// <summary>
        /// The account index, corresponding to e.g.: m/{0}'
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        public abstract List<ScriptPubKeyType> ScriptPubKeyTypes { get; set; }

        public Network Network { get; set; }
        public string Name { get; set; }
        public List<string> TxIds { get; set; }
        public List<Tx> Txs { get; set; }

        public abstract BitcoinAddress GetReceiveAddress();
        public abstract BitcoinAddress[] GetReceiveAddress(int n);
        public abstract BitcoinAddress GetChangeAddress();
        public abstract BitcoinAddress[] GetChangeAddress(int n);
        public abstract BitcoinAddress GetReceiveAddressAtIndex(int i);
        public abstract BitcoinAddress GetChangeAddressAtIndex(int i);
        public abstract BitcoinAddress[] GetReceiveAddressesToWatch();
        public abstract BitcoinAddress[] GetChangeAddressesToWatch();
        public abstract BitcoinAddress[] GetAddressesToWatch();

        public List<BitcoinAddress> UsedExternalAddresses { get; set; }
        public List<BitcoinAddress> UsedInternalAddresses { get; set; }
        public List<Coin> UnspentCoins { get; set; }
        public List<Coin> SpentCoins { get; set; }

        public abstract void AddTx(Tx tx);
        public abstract void UpdateTx(Tx tx);
        public abstract void RemoveTx(Tx tx);

        public abstract Money GetBalance();

        public event EventHandler<UpdatedConfirmationsArgs> OnUpdatedConfirmations;

        public object Clone()
        {
            return MemberwiseClone();
        }

        public int GetExternalLastIndex()
        {
            if (UsedExternalAddresses.Count() == 0) return 0;

            var acc = (HdAccount)Clone();
            acc.ExternalAddressesCount = 0;

            var lastAddress = acc.UsedExternalAddresses.Last();

            return GetExternalIndex(lastAddress);
        }

        public int GetInternalLastIndex()
        {
            if (UsedInternalAddresses.Count() == 0) return 0;

            var acc = (HdAccount)Clone();
            acc.InternalAddressesCount = 0;

            var lastAddress = acc.UsedInternalAddresses.Last();

            return GetInternalIndex(lastAddress);
        }

        public int GetExternalIndex(BitcoinAddress address)
        {
            var acc = (HdAccount)Clone();
            var addresses = acc.GetReceiveAddressesToWatch();

            for (int i = 0; i < addresses.Count(); i++)
            {
                var addr = addresses[i];

                if (address.Equals(addr)) return i;
            }

            throw new WalletException("Could not find external address, are you sure it's external?");
        }

        public int GetInternalIndex(BitcoinAddress address)
        {
            var acc = (HdAccount)Clone();
            var addresses = acc.GetChangeAddressesToWatch();

            for (int i = 0; i < addresses.Count(); i++)
            {
                var addr = addresses[i];

                if (address.Equals(addr)) return i;
            }

            throw new WalletException("Could not find internal address, are you sure it's internal?");
        }

        public void AddUtxo(Coin coin)
        {
            if (SpentCoins.Any(c => c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)) return;
            if (UnspentCoins.Any(c => c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)) return;

            UnspentCoins.Add(coin);
        }

        public void RemoveUtxo(Coin coin)
        {
            foreach (var c in UnspentCoins)
                if (c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)
                    UnspentCoins.Remove(c);

            if (SpentCoins.Any(c => c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)) return;

            SpentCoins.Add(coin);
        }

        public void UpdateConfirmations(long height)
        {
            foreach (var tx in Txs)
            {
                var txBlockHeight = tx.BlockHeight.GetValueOrDefault(0);

                if (txBlockHeight <= 0) continue;
                if (txBlockHeight >= height) continue;

                tx.Confirmations = height - txBlockHeight;

                OnUpdatedConfirmations?.Invoke(this, new UpdatedConfirmationsArgs(tx, tx.Confirmations));
            }
        }
    }
}
