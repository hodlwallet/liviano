//
// HdAccount.cs
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
using System.Collections.Generic;

using NBitcoin;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Liviano.Interfaces;
using Liviano.Models;
using System;

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
        int _GapLimit = 20;
        public int GapLimit
        {
            get => _GapLimit;
            set
            {
                if (value < 0) throw new ArgumentException($"Invalid value {value}");

                _GapLimit = value;
            }
        }

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
        public string ExtendedPubKey { get; set; }

        /// <summary>
        /// An extended pub key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPrivKey")]
        public string ExtendedPrivKey { get; set; }

        /// <summary>
        /// The account index, corresponding to e.g.: m/{0}'
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        [JsonProperty(PropertyName = "scriptPubKeyType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public abstract ScriptPubKeyType ScriptPubKeyType { get; set; }

        #region IAccountFields
        public Network Network { get; set; }
        public string Name { get; set; }
        public List<string> TxIds { get; set; }
        public List<Tx> Txs { get; set; }

        public abstract BitcoinAddress GetReceiveAddress();
        public abstract BitcoinAddress[] GetReceiveAddress(int n);
        public abstract BitcoinAddress GetChangeAddress();
        public abstract BitcoinAddress[] GetChangeAddress(int n);

        public abstract void AddTx(Tx tx);
        public abstract void UpdateTx(Tx tx);
        public abstract void RemoveTx(Tx tx);
        #endregion
    }
}