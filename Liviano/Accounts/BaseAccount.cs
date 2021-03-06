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
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using NBitcoin;

using Liviano.Interfaces;
using Liviano.Models;
using Liviano.Exceptions;
using Liviano.Events;
using Liviano.Extensions;
using System.Diagnostics;
using System.Collections.Specialized;

namespace Liviano.Accounts
{
    public abstract class BaseAccount : IAccount
    {
        static readonly object @lock = new();

        public string Id { get; set; }

        public abstract string AccountType { get; }

        public string WalletId { get; set; }

        public abstract int GapLimit { get; set; }

        public abstract int ExternalAddressesGapIndex { get; set; }

        public abstract int InternalAddressesGapIndex { get; set; }

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

        public ObservableCollection<string> TxIds { get; set; }
        public ObservableCollection<Tx> Txs { get; set; }

        public abstract BitcoinAddress GetReceiveAddress(int typeIndex = 0);
        public abstract BitcoinAddress[] GetReceiveAddress(int n, int typeIndex = 0);
        public abstract BitcoinAddress GetChangeAddress(int typeIndex = 0);
        public abstract BitcoinAddress[] GetChangeAddress(int n, int typeIndex = 0);
        public abstract BitcoinAddress GetReceiveAddressAtIndex(int i, int typeIndex = 0);
        public abstract BitcoinAddress GetChangeAddressAtIndex(int i, int typeIndex = 0);
        public abstract BitcoinAddress[] GetReceiveAddressesToWatch();
        public abstract BitcoinAddress[] GetChangeAddressesToWatch();
        public abstract BitcoinAddress[] GetAddressesToWatch();
        public abstract void GenerateNewAddresses();

        public Dictionary<ScriptPubKeyType, List<BitcoinAddressWithMetadata>> ExternalAddresses { get; set; }
        public Dictionary<ScriptPubKeyType, List<BitcoinAddressWithMetadata>> InternalAddresses { get; set; }

        public List<BitcoinAddress> UsedExternalAddresses { get; set; }
        public List<BitcoinAddress> UsedInternalAddresses { get; set; }

        public List<Coin> UnspentCoins { get; set; }
        public List<Coin> SpentCoins { get; set; }
        public List<Coin> FrozenCoins { get; set; }

        public long DustMinAmount { get; set; }

        public abstract void AddTx(Tx tx);
        public abstract void UpdateTx(Tx tx);
        public abstract void RemoveTx(Tx tx);

        public abstract Money GetBalance();

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
            lock (@lock)
            {
                if (SpentCoins.Any(c => c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)) return;
                if (UnspentCoins.Any(c => c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)) return;

                UnspentCoins.Add(coin);
            }

            if (coin.Amount < Money.FromUnit(DustMinAmount, MoneyUnit.Satoshi)) FreezeUtxo(coin);
        }

        public void RemoveUtxo(Coin coin)
        {
            lock (@lock)
            {
                foreach (var c in UnspentCoins.ToList())
                    if (c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)
                        UnspentCoins.Remove(c);

                if (SpentCoins.Any(c => c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)) return;

                SpentCoins.Add(coin);
            }
        }

        public void FreezeUtxo(Coin coin)
        {
            lock (@lock)
            {
                if (SpentCoins.Any(c => c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)) return;
                if (FrozenCoins.Any(c => c.Outpoint.Hash == coin.Outpoint.Hash && c.Outpoint.N == coin.Outpoint.N)) return;
                if (UnspentCoins.All(c => c.Outpoint.Hash != coin.Outpoint.Hash && c.Outpoint.N != coin.Outpoint.N)) return;

                FrozenCoins.Add(coin);
                UnspentCoins.Remove(coin);
            }
        }

        public void UnfreezeUtxo(Coin coin)
        {
            lock (@lock)
            {
                if (!FrozenCoins.Contains(coin)) return;

                FrozenCoins.Remove(coin);
                UnspentCoins.Add(coin);
            }
        }

        public void DeleteUtxo(Coin coin)
        {
            lock (@lock)
            {
                UnspentCoins.Remove(coin);
                FrozenCoins.Remove(coin);
                SpentCoins.Remove(coin);
            }
        }

        public void UpdateDustCoins()
        {
            if (DustMinAmount == -1) return;
            if (!UnspentCoins.Any()) return;

            lock (@lock)
            {
                foreach (var frozenCoin in FrozenCoins.ToList())
                    if (frozenCoin.Amount > Money.FromUnit(DustMinAmount, MoneyUnit.Satoshi)) UnfreezeUtxo(frozenCoin);

                foreach (var unspentCoin in UnspentCoins.ToList())
                    if (unspentCoin.Amount < Money.FromUnit(DustMinAmount, MoneyUnit.Satoshi)) FreezeUtxo(unspentCoin);

                FindAndRemoveDuplicateUtxo();
            }
        }

        public void UpdateUtxoListWithTransaction(Transaction transaction)
        {
            lock (@lock)
            {
                // Loop over the tx ids from the intputs of the transaction
                foreach (var input in transaction.Inputs.ToList())
                    // We check them with each outpoint of the transaction that's on the spent coins
                    foreach (var unspentCoin in UnspentCoins.ToList())
                        // if found, we remove that UTXO from being used
                        if (unspentCoin.Outpoint.Hash == input.PrevOut.Hash && unspentCoin.Outpoint.N == input.PrevOut.N)
                            RemoveUtxo(unspentCoin);
            }
        }

        public async void FindUtxosInTransactions()
        {
            var txs = Txs.ToList().Select(tx => tx.GetTransaction());

            bool CoinExist(ICoin coin)
            {
                return UnspentCoins.Contains(coin)
                    || SpentCoins.Contains(coin)
                    || FrozenCoins.Contains(coin);
            }

            Task FindUtxos(Transaction tx)
            {
                foreach (var coin in tx.Outputs.AsCoins())
                {
                    if (CoinExist(coin)) break;

                    var address = coin.ScriptPubKey.GetDestinationAddress(Network);

                    if (IsReceive(address) || IsChange(address)) AddUtxo(coin);
                }

                return Task.CompletedTask;
            }

            var addTasks = new List<Task> { };
            foreach (var tx in txs) addTasks.Add(FindUtxos(tx));

            await Task.WhenAll(addTasks);

            var utxos = UnspentCoins.ToList();

            Task FindSpents(Transaction tx)
            {
                foreach (var input in tx.Inputs)
                    foreach (var utxo in utxos)
                        if (input.PrevOut.Hash == utxo.Outpoint.Hash && input.PrevOut.N == utxo.Outpoint.N)
                            RemoveUtxo(utxo);

                return Task.CompletedTask;
            }

            var remTasks = new List<Task> { };
            foreach (var tx in txs) remTasks.Add(FindSpents(tx));

            await Task.WhenAll(remTasks);
        }

        public void FindAndRemoveDuplicateUtxo()
        {
            lock (@lock)
            {
                foreach (var spentCoin in SpentCoins.ToList())
                {
                    if (UnspentCoins.Contains(spentCoin))
                        UnspentCoins.Remove(spentCoin);
                }

                foreach (var frozenCoin in FrozenCoins.ToList())
                {
                    if (UnspentCoins.Contains(frozenCoin))
                        UnspentCoins.Remove(frozenCoin);
                }
            }
        }

        public bool ContainInputs(TxInList inputs)
        {
            foreach (var input in inputs)
            {
                foreach (var coin in UnspentCoins.ToList())
                    if (input.PrevOut.Hash == coin.Outpoint.Hash && input.PrevOut.N == coin.Outpoint.N)
                        return true;

                foreach (var coin in SpentCoins.ToList())
                    if (input.PrevOut.Hash == coin.Outpoint.Hash && input.PrevOut.N == coin.Outpoint.N)
                        return true;

                foreach (var coin in FrozenCoins.ToList())
                    if (input.PrevOut.Hash == coin.Outpoint.Hash && input.PrevOut.N == coin.Outpoint.N)
                        return true;
            }

            return false;
        }

        public void UpdateCreatedAtWithHeader(BlockHeader header, long height)
        {
            foreach (var tx in Txs)
            {
                if (tx.Height <= 0) continue;
                if (tx.Height > height) continue;

                if (tx.Height == height)
                {
                    tx.CreatedAt = header.BlockTime;

                    OnUpdatedTxCreatedAt?.Invoke(this, new UpdatedTxCreatedAtArgs(tx, tx.CreatedAt));
                    continue;
                }

                //tx.CreatedAt = GetAproxTime(height, tx.Height, header);
                //OnUpdatedTxCreatedAt?.Invoke(this, new UpdatedTxCreatedAtArgs(tx, tx.CreatedAt));
            }

        }

        public bool IsReceive(BitcoinAddress address)
        {
            lock (@lock)
            {
                foreach (var spkt in ScriptPubKeyTypes)
                    if (ExternalAddresses[spkt].ToList().Any(addrWithData => address == addrWithData.Address))
                        return true;

                return false;
            }
        }

        public bool IsChange(BitcoinAddress address)
        {
            lock (@lock)
            {
                foreach (var spkt in ScriptPubKeyTypes)
                    if (InternalAddresses[spkt].ToList().Any(addrWithData => address == addrWithData.Address))
                        return true;

                return false;
            }
        }

        static DateTimeOffset GetAproxTime(long currentBlockHeight, long txBlockHeight, BlockHeader header)
        {
            var blocksApart = currentBlockHeight - txBlockHeight;
            var minutes = blocksApart * 10;

            return header.BlockTime - TimeSpan.FromMinutes(minutes);
        }
    }
}
