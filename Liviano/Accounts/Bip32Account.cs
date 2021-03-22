//
// Bip32Account.cs
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

        protected Bip32Account(Network network, int index = 0)
        {
            Id = Guid.NewGuid().ToString();

            TxIds ??= new List<string>();
            Txs ??= new List<Tx>();

            InternalAddressesCount = 0;
            ExternalAddressesCount = 0;

            UsedExternalAddresses ??= new List<BitcoinAddress>();
            UsedInternalAddresses ??= new List<BitcoinAddress>();

            UnspentCoins ??= new List<Coin>();
            SpentCoins ??= new List<Coin>();

            Network = network;

            Index = index;
            HdPath = GetHdPath();
        }

        public override BitcoinAddress GetReceiveAddress()
        {
            var pubKey = Hd.GeneratePublicKey(Network, ExtPubKey.ToString(), ExternalAddressesCount, false);
            var address = pubKey.GetAddress(ScriptPubKeyTypes[0], Network); // TODO

            ExternalAddressesCount++;

            if (IsAddressReuse(address, isChange: false)) return GetReceiveAddress();

            return address;
        }

        public override BitcoinAddress[] GetReceiveAddress(int n)
        {
            var addresses = new List<BitcoinAddress>();

            for (int i = 0; i < n; i++)
                addresses.Add(GetReceiveAddress());

            return addresses.ToArray();
        }

        public override BitcoinAddress GetChangeAddress()
        {
            var pubKey = Hd.GeneratePublicKey(Network, ExtPubKey.ToString(), InternalAddressesCount, true);
            var address = pubKey.GetAddress(ScriptPubKeyTypes[0], Network); // TODO

            InternalAddressesCount++;

            if (IsAddressReuse(address, isChange: true)) return GetChangeAddress();

            return address;
        }

        public override BitcoinAddress[] GetChangeAddress(int n)
        {
            var addresses = new List<BitcoinAddress>();

            for (int i = 0; i < n; i++)
                addresses.Add(GetChangeAddress());

            return addresses.ToArray();
        }

        public override BitcoinAddress GetReceiveAddressAtIndex(int i)
        {
            var pubKey = Hd.GeneratePublicKey(Network, ExtPubKey.ToString(), i, false);
            var address = pubKey.GetAddress(ScriptPubKeyTypes[0], Network); // TODO

            return address;
        }

        public override BitcoinAddress GetChangeAddressAtIndex(int i)
        {
            var pubKey = Hd.GeneratePublicKey(Network, ExtPubKey.ToString(), i, true);
            var address = pubKey.GetAddress(ScriptPubKeyTypes[0], Network); // TODO

            return address;
        }

        public override BitcoinAddress[] GetReceiveAddressesToWatch()
        {
            var addresses = new List<BitcoinAddress> { };

            var externalMaxIndex = ExternalAddressesIndex + GapLimit;

            for (int i = 0; i < externalMaxIndex; i++)
            {
                var pubKey = Hd.GeneratePublicKey(Network, ExtPubKey.ToString(), i, false);
                var addr = pubKey.GetAddress(ScriptPubKeyTypes[0], Network); // TODO

                addresses.Add(addr);
            }

            return addresses.ToArray();
        }

        public override BitcoinAddress[] GetChangeAddressesToWatch()
        {
            var addresses = new List<BitcoinAddress> { };

            var internalMaxIndex = InternalAddressesIndex + GapLimit;

            for (int i = 0; i < internalMaxIndex; i++)
            {
                var pubKey = Hd.GeneratePublicKey(Network, ExtPubKey.ToString(), i, true);
                var addr = pubKey.GetAddress(ScriptPubKeyTypes[0], Network); // TODO

                addresses.Add(addr);
            }

            return addresses.ToArray();
        }

        public override BitcoinAddress[] GetAddressesToWatch()
        {
            var addresses = new List<BitcoinAddress> { };

            addresses.AddRange(GetReceiveAddressesToWatch());
            addresses.AddRange(GetChangeAddressesToWatch());

            return addresses.ToArray();
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

        /// <summary>
        /// Creates a account based on a type string
        /// </summary>
        /// <param name="name">Name of the account</param>
        /// <returns></returns>
        public static Bip32Account Create(string name, object options)
        {
            Guard.NotEmpty(name, nameof(name));

            var kwargs = options.ToDict();

            var type = (string)kwargs.TryGet("Type");
            var network = (Network)kwargs.TryGet("Network");
            var wallet = (IWallet)kwargs.TryGet("Wallet");
            var index = (int)kwargs.TryGet("Index");

            Bip32Account account = type switch
            {
                "bip44" => new Bip44Account(network, index),
                "bip49" => new Bip49Account(network, index),
                "bip84" => new Bip84Account(network, index),
                "bip141" => new Bip141Account(network, index),
                _ => null,
            };

            if (account is null)
                throw new ArgumentException($"Incorrect account type: {type}");

            account.Name = name;
            account.Wallet = wallet;
            account.WalletId = wallet.Id;
            account.Network = network;
            account.Index = index;
            account.HdPath = account.GetHdPath();

            var extPrivKey = wallet.GetExtendedKey().Derive(new KeyPath(account.HdPath));
            var extPubKey = extPrivKey.Neuter();

            account.ExtKey = extPrivKey;
            account.ExtPubKey = extPubKey;

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

        string GetHdPath()
        {
            if (HdPathFormat.Split('/').Length < 2) return string.Format(HdPathFormat, Index);

            return string.Format(HdPathFormat, Network.Equals(Network.Main) ? 0 : 1, Index);
        }

        bool IsAddressReuse(BitcoinAddress address, bool isChange = false)
        {
            if (isChange) return UsedInternalAddresses.Any(addr => string.Equals(addr.ToString(), address.ToString()));
            else return UsedExternalAddresses.Any(addr => string.Equals(addr.ToString(), address.ToString()));
        }

    }
}
