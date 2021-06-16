//
// TransactionExtensions.cs
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
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

using NBitcoin;

using Liviano.Interfaces;
using Liviano.Exceptions;
using Liviano.Models;

namespace Liviano.Extensions
{
    public static class TransactionExtensions
    {
        static Key[] GetCoinsKeys(ICoin[] coins, IAccount account)
        {
            return coins.Select(o => GetCoinKey(o, account)).ToArray();
        }

        static Key GetCoinKey(ICoin coin, IAccount account)
        {
            var tx = account.Txs.FirstOrDefault(o => o.Id == coin.Outpoint.Hash);
            var transaction = Transaction.Parse(tx.Hex, account.Network);
            var output = transaction.Outputs[coin.Outpoint.N];
            var addr = output.ScriptPubKey.GetDestinationAddress(account.Network);

            int change = tx.IsSend ? 1 : 0;
            int index;
            if (change == 0)
                index = account.GetExternalIndex(addr);
            else
                index = account.GetInternalIndex(addr);

            var keyPath = new KeyPath($"{change}/{index}");
            var extKey = account.ExtKey.Derive(keyPath);

            return extKey.PrivateKey;
        }

        static ICoin[] GetCoinsFromTransaction(Transaction tx, IAccount account)
        {
            var coins = new List<Coin>();

            foreach (var input in tx.Inputs)
            {
                foreach (var unspentCoin in account.UnspentCoins.ToList())
                    if (unspentCoin.Outpoint.Hash == input.PrevOut.Hash && unspentCoin.Outpoint.N == input.PrevOut.N)
                        coins.Add(unspentCoin);
                foreach (var spentCoin in account.SpentCoins.ToList())
                    if (spentCoin.Outpoint.Hash == input.PrevOut.Hash && spentCoin.Outpoint.N == input.PrevOut.N)
                        coins.Add(spentCoin);
                foreach (var frozenCoin in account.FrozenCoins.ToList())
                    if (frozenCoin.Outpoint.Hash == input.PrevOut.Hash && frozenCoin.Outpoint.N == input.PrevOut.N)
                        coins.Add(frozenCoin);
            }

            return coins.ToArray();
        }

        public static Transaction BumpFee(
                decimal satsPerByte,
                Tx tx,
                IAccount account)
        {
            if (tx.IsReceive) throw new WalletException("[BumpFee] Bump fee failed because transaction was not sent by yourself");

            var builder = account.Network.CreateTransactionBuilder();
            var transaction = Transaction.Parse(tx.Hex, account.Network);

            if (!transaction.RBF) throw new WalletException("[BumpFee] Bump fee failed because transaction is not RBF");

            var coins = GetCoinsFromTransaction(transaction, account);
            var keys = GetCoinsKeys(coins, account);

            builder.AddCoins(coins);
            builder.AddKeys(keys);

            foreach (var output in transaction.Outputs.ToList())
            {
                var destinationAddress = output.ScriptPubKey.GetDestinationAddress(account.Network);
                var amount = output.Value;
                bool isInternal = false;

                foreach (var spkt in account.ScriptPubKeyTypes)
                    if (account.InternalAddresses[spkt].ToList().Any(o => o.Address.Equals(destinationAddress)))
                        isInternal = true;

                if (isInternal)
                {
                    builder.SetChange(destinationAddress);
                    builder.SubtractFees();
                }
                else
                    builder.Send(destinationAddress, amount);
            }

            builder.SetOptInRBF(true);
            builder.SendEstimatedFees(new FeeRate(satsPerByte));

            transaction = builder.BuildTransaction(sign: true);

            tx.ReplacedId = transaction.GetHash();

            VerifyTransaction(builder, transaction, out var errors);

            if (errors.Any())
            {
                string error = string.Join<string>(", ", errors.Select(o => o.Message));

                Debug.WriteLine($"[CreateTransaction] Error: {error}");

                throw new WalletException(error);
            }

            return transaction;
        }

        public static (TransactionBuilder builder, Transaction tx) CreateTransaction(
                string destinationAddress,
                Money amount,
                decimal satsPerByte,
                bool rbf,
                IAccount account)
        {
            // Get coins from coin selector that satisfy our amount.
            var coinSelector = new DefaultCoinSelector();
            var coins = coinSelector.Select(account.UnspentCoins, amount).ToArray();

            if (coins.Count() == 0) throw new WalletException("Balance too low to create transaction.");

            var changeDestination = account.GetChangeAddress();
            var toDestination = BitcoinAddress.Create(destinationAddress, account.Network);

            var keys = GetCoinsKeys(coins, account);

            Debug.WriteLine($"[CreateTransaction] Coins: {string.Join(",", coins.Select(o => $"{o.Outpoint.Hash}-{o.Outpoint.N}"))}");
            Debug.WriteLine($"[CreateTransaction] Keys: {string.Join(",", keys.Select(o => $"{o.GetWif(account.Network)}"))}");

            var builder = account.Network.CreateTransactionBuilder();

            // Build the tx
            builder.AddCoins(coins);
            builder.AddKeys(keys);
            builder.Send(toDestination, amount);
            builder.SetChange(changeDestination);
            builder.SetOptInRBF(rbf);
            builder.SendEstimatedFees(new FeeRate(satsPerByte));

            // Create transaction builder
            var tx = builder.BuildTransaction(sign: true);

            Debug.WriteLine($"[CreateTransaction] Tx: {tx.ToHex()}");

#if DEBUG
            foreach (var input in tx.Inputs)
                Debug.WriteLine($"[CreateTransaction] Inputs: {input.PrevOut.Hash}-{input.PrevOut.N}");
#endif

            return (builder, tx);
        }

        public static bool VerifyTransaction(
                TransactionBuilder builder,
                Transaction tx,
                out WalletException[] transactionPolicyErrors)
        {
            var flag = builder.Verify(tx, out var errors);
            var exceptions = new List<WalletException>();

            if (errors.Any())
            {
                foreach (var error in errors)
                    exceptions.Add(new WalletException(error.ToString()));
            }

            transactionPolicyErrors = exceptions.ToArray();

            return flag;
        }
    }
}
