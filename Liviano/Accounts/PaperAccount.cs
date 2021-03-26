﻿//
// PaperAccount.cs
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
using System.Diagnostics;

using Newtonsoft.Json;

using NBitcoin;

using Liviano.Utilities;
using Liviano.Extensions;
using Liviano.Models;

namespace Liviano.Accounts
{
    public class PaperAccount : BaseAccount
    {
        public override string AccountType => "paper";

        int gapLimit;
        public override int GapLimit
        {
            get
            {
                return gapLimit;
            }

            set
            {
                // Paper accounts have no Gap limit!
                Guard.Assert(value == 0);

                gapLimit = value;
            }
        }

        public const ScriptPubKeyType DEFAULT_SCRIPT_PUB_KEY_TYPE = ScriptPubKeyType.Segwit;

        public override List<ScriptPubKeyType> ScriptPubKeyTypes { get; set; }

        [JsonProperty(PropertyName = "privateKey")]
        public Key PrivateKey { get; set; }

        [JsonProperty(PropertyName = "publicKey")]
        public PubKey PublicKey { get; set; }

        public override int InternalAddressesCount
        {
            get => 0;
            set { }
        }

        public override int ExternalAddressesCount
        {
            get => 1;
            set { }
        }

        public new string HdPath { get => null; set => throw new ArgumentException($"Invalid cannot set to {value}"); }
        public new BitcoinExtPubKey ExtPubKey { get => null; set => throw new ArgumentException($"Invalid cannot set to {value}"); }
        public new BitcoinExtKey ExtKey { get => null; set => throw new ArgumentException($"Invalid cannot set to {value}"); }
        public override int InternalAddressesIndex { get; set; }
        public override int ExternalAddressesIndex { get; set; }

        public PaperAccount(string name, string wif = null, Network network = null, int index = 0)
        {
            Guard.NotNull(name, nameof(name));

            var scriptPubKeyType = DEFAULT_SCRIPT_PUB_KEY_TYPE;

            TxIds ??= new List<string>();
            Txs ??= new List<Tx>();

            UsedExternalAddresses ??= new List<BitcoinAddress>();
            UsedInternalAddresses ??= new List<BitcoinAddress>();

            SpentCoins ??= new List<Coin>();
            UnspentCoins ??= new List<Coin>();

            Index = index;

            Initialize(name, scriptPubKeyType, wif, network);
        }

        public PaperAccount(string name, ScriptPubKeyType scriptPubKeyType, string wif = null, Network network = null, int index = 0)
        {
            Index = index;

            Initialize(name, scriptPubKeyType, wif, network);
        }

        public override BitcoinAddress GetChangeAddress(int typeIndex = 0)
        {
            throw new ArgumentException("Paper accounts cannot generate change addresses, you must swippe then into another account!");
        }

        public override BitcoinAddress[] GetChangeAddress(int n, int typeIndex = 0)
        {
            throw new ArgumentException("Paper accounts cannot generate change addresses, you must swippe then into another account!");
        }

        public override BitcoinAddress GetReceiveAddress(int typeIndex = 0)
        {
            Guard.NotNull(PublicKey, nameof(PublicKey));

            return PublicKey.GetAddress(ScriptPubKeyTypes[typeIndex], Network);
        }

        public override BitcoinAddress[] GetReceiveAddress(int n, int typeIndex = 0)
        {
            throw new ArgumentException("Paper accounts cannot generate more than 1 address, use method without parameters");
        }

        public override BitcoinAddress GetReceiveAddressAtIndex(int i, int typeIndex = 0)
        {
            throw new ArgumentException("Paper accounts cannot generate more than 1 address!");
        }

        public override BitcoinAddress GetChangeAddressAtIndex(int i, int typeIndex = 0)
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
            ScriptPubKeyTypes = new List<ScriptPubKeyType>() { scriptPubKeyType };
            Network ??= network ?? Network.Main;

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

        public override void AddTx(Tx tx)
        {
            if (TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"[AddTx] Wallet already has a tx with id: {tx.Id}");

                return;
            }

            TxIds.Add(tx.Id.ToString());
            Txs.Add(tx);
        }

        public override void RemoveTx(Tx tx)
        {
            if (!TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"[RemoveTx] Wallet doesn't have tx with id: {tx.Id}");

                return;
            }

            Txs.Remove(tx);
            TxIds.Remove(tx.Id.ToString());
        }

        public override void UpdateTx(Tx tx)
        {
            if (!TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"[UpdateTx] Wallet doesn't have tx with id: {tx.Id}");

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

        public override Money GetBalance()
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

        public new int GetExternalLastIndex()
        {
            return 0;
        }

        public new int GetInternalLastIndex()
        {
            throw new ArgumentException("Paper accounts cannot generate change addresses, you must swippe then into another account!");
        }

        public override BitcoinAddress[] GetReceiveAddressesToWatch()
        {
            return new BitcoinAddress[] { GetReceiveAddress() };
        }

        public override BitcoinAddress[] GetChangeAddressesToWatch()
        {
            throw new ArgumentException("Paper accounts cannot generate change addresses, you must swippe then into another account!");
        }

        public Coin[] GetSpendableCoins()
        {
            var address = GetReceiveAddress();
            var coins = new List<Coin> {};

            foreach (var tx in Txs)
            {
                var transaction = Transaction.Parse(tx.Hex, Network);

                foreach (var outCoin in transaction.Outputs.AsCoins())
                {
                    var destinationAddress = outCoin.ScriptPubKey.GetDestinationAddress(Network);

                    if (destinationAddress.Equals(address)) coins.Add(outCoin);
                }
            }

            return coins.ToArray();
        }

        public new int GetExternalIndex(BitcoinAddress address)
        {
            return 0;
        }

        public new int GetInternalIndex(BitcoinAddress address)
        {
            throw new ArgumentException("Paper accounts cannot generate change addresses, you must swippe then into another account!");
        }

        public override BitcoinAddress[] GetAddressesToWatch()
        {
            return GetReceiveAddressesToWatch();
        }

        public new void AddUtxo(Coin coin)
        {
            if (SpentCoins.Contains(coin)) return;
            if (UnspentCoins.Contains(coin)) return;

            UnspentCoins.Add(coin);
        }

        public new void RemoveUtxo(Coin coin)
        {
            if (SpentCoins.Contains(coin)) return;
            if (UnspentCoins.Contains(coin)) UnspentCoins.Remove(coin);

            SpentCoins.Add(coin);
        }
    }
}
