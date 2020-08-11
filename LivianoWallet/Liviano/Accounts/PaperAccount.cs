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

using Newtonsoft.Json;

using NBitcoin;

using Liviano.Interfaces;
using Liviano.Utilities;
using Liviano.Utilities.JsonConverters;
using Newtonsoft.Json.Converters;
using Liviano.Extensions;
using Liviano.Models;
using System.Diagnostics;

namespace Liviano.Accounts
{
    public class PaperAccount : IAccount
    {
        public string AccountType => "paper";

        int _GapLimit;
        public int GapLimit
        {
            get
            {
                return _GapLimit;
            }

            set
            {
                // Paper accounts have no Gap limit!
                Guard.Assert(value == 0);

                _GapLimit = value;
            }
        }

        public const ScriptPubKeyType DEFAULT_SCRIPT_PUB_KEY_TYPE = ScriptPubKeyType.Segwit;

        public string Id { get; set; }

        public string WalletId { get; set; }

        public Network Network { get; set; }

        public string Name { get; set; }

        public List<string> TxIds { get; set; }

        public List<Tx> Txs { get; set; }

        [JsonProperty(PropertyName = "scriptPubKeyType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScriptPubKeyType ScriptPubKeyType { get; set; }

        [JsonProperty(PropertyName = "privateKey")]
        [JsonConverter(typeof(PrivateKeyConverter))]
        public Key PrivateKey { get; set; }

        [JsonProperty(PropertyName = "publicKey")]
        [JsonConverter(typeof(PublicKeyConverter))]
        public PubKey PublicKey { get; set; }

        public int InternalAddressesCount
        {
            get => 0;
            set { }
        }

        public int ExternalAddressesCount
        {
            get => 1;
            set { }
        }

        public int Index { get; set; }

        public IWallet Wallet { get; set; }

        public string HdPath { get => null; set => throw new ArgumentException($"Invalid cannot set to {value}"); }
        public string ExtendedPubKey { get => null; set => throw new ArgumentException($"Invalid cannot set to {value}"); }
        public string ExtendedPrivKey { get => null; set => throw new ArgumentException($"Invalid cannot set to {value}"); }
        public List<BitcoinAddress> UsedExternalAddresses { get; set; }
        public List<BitcoinAddress> UsedInternalAddresses { get; set; }

        public PaperAccount(string name, string wif = null, Network network = null, int index = 0)
        {
            Guard.NotNull(name, nameof(name));

            var scriptPubKeyType = DEFAULT_SCRIPT_PUB_KEY_TYPE;

            TxIds = TxIds ?? new List<string>();
            Txs = Txs ?? new List<Tx>();

            UsedExternalAddresses = UsedExternalAddresses ?? new List<BitcoinAddress>();
            UsedInternalAddresses = UsedInternalAddresses ?? new List<BitcoinAddress>();

            Index = index;

            Initialize(name, scriptPubKeyType, wif, network);
        }

        public PaperAccount(string name, ScriptPubKeyType scriptPubKeyType, string wif = null, Network network = null, int index = 0)
        {
            Index = index;

            Initialize(name, scriptPubKeyType, wif, network);
        }

        public BitcoinAddress GetChangeAddress()
        {
            throw new ArgumentException("Paper accounts cannot generate change addresses, you must swippe then into anothen account!");
        }

        public BitcoinAddress[] GetChangeAddress(int n)
        {
            throw new ArgumentException("Paper accounts cannot generate change addresses, you must swippe then into anothen account!");
        }

        public BitcoinAddress GetReceiveAddress()
        {
            Guard.NotNull(PublicKey, nameof(PublicKey));

            return PublicKey.GetAddress(ScriptPubKeyType, Network);
        }

        public BitcoinAddress[] GetReceiveAddress(int n)
        {
            throw new ArgumentException("Paper accounts cannot generate more than 1 address, use method without parameters");
        }

        public BitcoinAddress GetReceiveAddressAtIndex(int i)
        {
            throw new ArgumentException("Paper accounts cannot generate more than 1 address!");
        }

        public BitcoinAddress GetChangeAddressAtIndex(int i)
        {
            throw new ArgumentException("Paper accounts cannot generate more than 1 address!");
        }

        public static PaperAccount Create(string name, object options)
        {
            var kwargs = options.ToDict();

            var wif = (string)kwargs.TryGet("Wif");
            var network = (Network)kwargs.TryGet("Network");

            Guard.NotNull(network, nameof(network));

            ScriptPubKeyType scriptPubKeyType = kwargs.ContainsKey("ScriptPubKeyType")
                ? (ScriptPubKeyType)kwargs["ScriptPubKeyType"]
                : DEFAULT_SCRIPT_PUB_KEY_TYPE;

            var account = new PaperAccount(
                name,
                scriptPubKeyType,
                wif,
                network
            );

            return account;
        }

        void Initialize(string name, ScriptPubKeyType scriptPubKeyType, string wif = null, Network network = null)
        {
            Guard.NotNull(name, nameof(name));
            Guard.NotNull(scriptPubKeyType, nameof(scriptPubKeyType));

            Id = Guid.NewGuid().ToString();
            Name = name;
            ScriptPubKeyType = scriptPubKeyType;
            Network = Network ?? network ?? Network.Main;

            if (wif is null)
            {
                PrivateKey = new Key();
                PublicKey = PrivateKey.PubKey;
            }
            else
            {
                PrivateKey = Key.Parse(wif, Network);
                PublicKey = PrivateKey.PubKey;
            }
        }

        public void AddTx(Tx tx)
        {
            if (TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"Wallet already has a tx with id: {tx.Id}");

                return;
            }

            TxIds.Add(tx.Id.ToString());
            Txs.Add(tx);
        }

        public void RemoveTx(Tx tx)
        {
            if (!TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"Wallet doesn't have tx with id: {tx.Id}");

                return;
            }

            Txs.Remove(tx);
            TxIds.Remove(tx.Id.ToString());
        }

        public void UpdateTx(Tx tx)
        {
            if (!TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"Wallet doesn't have tx with id: {tx.Id}");

                return;
            }

            for (int i = 0, count = Txs.Count; i < count; i++)
            {
                if (Txs[i].Id == tx.Id)
                {
                    Txs[i] = tx;
                    break;
                }
            }
        }

        public Money GetBalance()
        {
            var received = Money.Zero;
            var sent = Money.Zero;

            foreach (var tx in Txs)
            {
                if (tx.IsSend)
                {
                    sent += tx.AmountSent;
                }
                else
                {
                    received += tx.AmountReceived;
                }
            }

            return received - sent;
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
