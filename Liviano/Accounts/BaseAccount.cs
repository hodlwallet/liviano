//
// BaseAccount.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2021 HODL Wallet
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

using Liviano.Interfaces;
using Liviano.Models;
using Liviano.Exceptions;
using Liviano.Events;
using Liviano.Extensions;

namespace Liviano.Accounts
{
    public abstract class BaseAccount : IAccount
    {
        public string Id { get; set; }

        public abstract string AccountType { get; }

        public string WalletId { get; set; }

        public abstract int GapLimit { get; set; }

        int ExternalAddressesGapIndex { get; set; }

        int InternalAddressesGapIndex { get; set; }

        public abstract int InternalAddressesIndex { get; set; }

        public abstract int ExternalAddressesIndex { get; set; }

        public IWallet Wallet { get; set; }

        public string HdPath { get; set; }

        public BitcoinExtPubKey ExtPubKey { get; set; }

        public BitcoinExtKey ExtKey { get; set; }

        public int Index { get; set; }

        public abstract List<ScriptPubKeyType> ScriptPubKeyTypes { get; set; }

        public Network Network { get; set; }
        public string Name { get; set; }
        public List<string> TxIds { get; set; }
        public List<Tx> Txs { get; set; }

        public abstract BitcoinAddress GetReceiveAddress(int typeIndex = 0);
        public abstract BitcoinAddress[] GetReceiveAddress(int n, int typeIndex = 0);
        public abstract BitcoinAddress GetChangeAddress(int typeIndex = 0);
        public abstract BitcoinAddress[] GetChangeAddress(int n, int typeIndex = 0);
        public abstract BitcoinAddress GetReceiveAddressAtIndex(int i, int typeIndex = 0);
        public abstract BitcoinAddress GetChangeAddressAtIndex(int i, int typeIndex = 0);
        public abstract BitcoinAddress[] GetReceiveAddressesToWatch();
        public abstract BitcoinAddress[] GetChangeAddressesToWatch();
        public abstract BitcoinAddress[] GetAddressesToWatch();

        public Dictionary<ScriptPubKeyType, List<BitcoinAddressWithMetadata>> ExternalAddresses { get; set; }
        public Dictionary<ScriptPubKeyType, List<BitcoinAddressWithMetadata>> InternalAddresses { get; set; }

        public List<BitcoinAddress> UsedExternalAddresses { get; set; }
        public List<BitcoinAddress> UsedInternalAddresses { get; set; }
        public List<Coin> UnspentCoins { get; set; }
        public List<Coin> SpentCoins { get; set; }

        public abstract void AddTx(Tx tx);
        public abstract void UpdateTx(Tx tx);
        public abstract void RemoveTx(Tx tx);

        public abstract Money GetBalance();

        public event EventHandler<UpdatedTxConfirmationsArgs> OnUpdatedTxConfirmations;
        public event EventHandler<UpdatedTxCreatedAtArgs> OnUpdatedTxCreatedAt;

        public object Clone()
        {
            return MemberwiseClone();
        }

        public int GetExternalLastIndex()
        {
            int index = 0;

            foreach (var usedAddress in UsedExternalAddresses)
            {
                var addressIndex = GetExternalIndex(usedAddress);

                if (addressIndex > index) index = addressIndex;
            }

            return index;
        }

        public int GetInternalLastIndex()
        {
            int index = 0;

            foreach (var usedAddress in UsedInternalAddresses)
            {
                var addressIndex = GetInternalIndex(usedAddress);

                if (addressIndex > index) index = addressIndex;
            }

            return index;
        }

        public int GetExternalIndex(BitcoinAddress address)
        {
            foreach (var spkt in ScriptPubKeyTypes)
            {
                if (!address.IsScriptPubKeyType(spkt)) continue;

                for (int i = 0; i < ExternalAddresses[spkt].Count; i++)
                    if (ExternalAddresses[spkt][i].Address == address) return i;
            }

            throw new WalletException("Could not find external address, are you sure it's external?");
        }

        public int GetInternalIndex(BitcoinAddress address)
        {
            foreach (var spkt in ScriptPubKeyTypes)
            {
                if (!address.IsScriptPubKeyType(spkt)) continue;

                for (int i = 0; i < InternalAddresses[spkt].Count; i++)
                    if (InternalAddresses[spkt][i].Address == address) return i;
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

                OnUpdatedTxConfirmations?.Invoke(this, new UpdatedTxConfirmationsArgs(tx, tx.Confirmations));
            }
        }

        public void UpdateCreatedAtWithHeader(BlockHeader header, long height)
        {
            foreach (var tx in Txs)
            {
                var txCreatedAt = tx.CreatedAt.GetValueOrDefault();

                if (
                    !DateTimeOffset.Equals(txCreatedAt, default(DateTimeOffset)) ||
                    !tx.HasAproxCreatedAt
                ) continue;

                var txBlockHeight = tx.BlockHeight.GetValueOrDefault(0);

                if (txBlockHeight <= 0) continue;
                if (txBlockHeight > height) continue;

                if (txBlockHeight == height)
                {
                    tx.CreatedAt = header.BlockTime;
                    tx.HasAproxCreatedAt = false;

                    OnUpdatedTxCreatedAt?.Invoke(this, new UpdatedTxCreatedAtArgs(tx, tx.CreatedAt));
                    continue;
                }

                tx.CreatedAt = GetAproxTime(height, txBlockHeight, header, tx);
                tx.HasAproxCreatedAt = true;

                OnUpdatedTxCreatedAt?.Invoke(this, new UpdatedTxCreatedAtArgs(tx, tx.CreatedAt));
            }

        }

        DateTimeOffset GetAproxTime(long currentBlockHeight, long txBlockHeight, BlockHeader header, Tx tx)
        {
            var blocksApart = currentBlockHeight - txBlockHeight;
            var minutes = blocksApart * 10;

            return header.BlockTime - TimeSpan.FromMinutes(minutes);
        }
    }
}
