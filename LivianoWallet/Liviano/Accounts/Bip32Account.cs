//
// Bip32Account.cs
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
using System.Diagnostics;

using NBitcoin;

using Liviano.Bips;
using Liviano.Models;
using Liviano.Extensions;
using Liviano.Interfaces;
using Liviano.Utilities;

namespace Liviano.Accounts
{
    public abstract class Bip32Account : HdAccount
    {
        public abstract string HdPathFormat { get; }

        protected Bip32Account(int index = 0)
        {
            Id = Guid.NewGuid().ToString();

            TxIds = TxIds ?? new List<string>();
            Txs = Txs ?? new List<Tx>();

            InternalAddressesCount = 0;
            ExternalAddressesCount = 0;

            UsedExternalAddresses = UsedExternalAddresses ?? new List<BitcoinAddress>();
            UsedInternalAddresses = UsedInternalAddresses ?? new List<BitcoinAddress>();

            Index = index;
            HdPath = string.Format(HdPathFormat, Index);
        }

        public override BitcoinAddress GetReceiveAddress()
        {
            var pubKey = Hd.GeneratePublicKey(ExtendedPubKey, ExternalAddressesCount, false);
            var address = pubKey.GetAddress(ScriptPubKeyType, Network);

            ExternalAddressesCount++;

            return address;
        }

        public override BitcoinAddress[] GetReceiveAddress(int n)
        {
            var addresses = new List<BitcoinAddress>();

            for (int i = 0; i < n; i++)
            {
                var pubKey = Hd.GeneratePublicKey(ExtendedPubKey, ExternalAddressesCount, false);

                addresses.Add(pubKey.GetAddress(ScriptPubKeyType, Network));

                ExternalAddressesCount++;
            }

            return addresses.ToArray();
        }

        public override BitcoinAddress GetChangeAddress()
        {
            var pubKey = Hd.GeneratePublicKey(ExtendedPubKey, InternalAddressesCount, true);
            var address = pubKey.GetAddress(ScriptPubKeyType, Network);

            InternalAddressesCount++;

            return address;
        }

        public override BitcoinAddress[] GetChangeAddress(int n)
        {
            var addresses = new List<BitcoinAddress>();

            for (int i = 0; i < n; i++)
            {
                var pubKey = Hd.GeneratePublicKey(ExtendedPubKey, InternalAddressesCount, true);

                addresses.Add(pubKey.GetAddress(ScriptPubKeyType, Network));

                InternalAddressesCount++;
            }

            return addresses.ToArray();
        }

        public override void AddTx(Tx tx)
        {
            if (TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"Wallet already has a tx with id: {tx.Id}");

                return;
            }

            TxIds.Add(tx.Id.ToString());
            Txs.Add(tx);
        }

        public override void RemoveTx(Tx tx)
        {
            if (!TxIds.Contains(tx.Id.ToString()))
            {
                Debug.WriteLine($"Wallet doesn't have tx with id: {tx.Id}");

                return;
            }

            Txs.Remove(tx);
            TxIds.Remove(tx.Id.ToString());
        }

        public override void UpdateTx(Tx tx)
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

        /// <summary>
        /// Creates a account based on a type string
        /// </summary>
        /// <param name="name">Name of the account</param>
        /// <returns></returns>
        public static Bip32Account Create(string name, (string, string) colors, object options)
        {
            Guard.NotEmpty(name, nameof(name));

            var kwargs = options.ToDict();

            var type = (string)kwargs.TryGet("Type");
            var network = (Network)kwargs.TryGet("Network");
            var wallet = (IWallet)kwargs.TryGet("Wallet");

            Bip32Account account;
            switch (type)
            {
                case "bip44":
                    account = new Bip44Account();
                    break;
                case "bip49":
                    account = new Bip49Account();
                    break;
                case "bip84":
                    account = new Bip84Account();
                    break;
                case "bip141":
                    account = new Bip141Account();
                    break;
                case "wasabi":
                    // This makes very little sence, but it's here just in case
                    account = new WasabiAccount();
                    break;
                default:
                    account = null;
                    break;
            }

            if (account is null)
                throw new ArgumentException($"Incorrect account type: {type}");

            account.Name = name;
            account.Wallet = wallet;
            account.WalletId = wallet.Id;
            account.Network = network;
            account.StartHex = colors.Item1;
            account.EndHex = colors.Item2;

            var extPrivKey = wallet.GetExtendedKey().Derive(new KeyPath(account.HdPath));
            var extPubKey = extPrivKey.Neuter();

            account.ExtendedPrivKey = extPrivKey.ToString(network);
            account.ExtendedPubKey = extPubKey.ToString(network);

            return account;
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
    }
}