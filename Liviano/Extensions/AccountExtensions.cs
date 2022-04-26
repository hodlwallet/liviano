//
// AccountExtensions.cs
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
using System.Linq;

using NBitcoin;

using Liviano.Accounts;
using Liviano.Bips;
using Liviano.Exceptions;
using Liviano.Interfaces;
using Liviano.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using static Liviano.Electrum.ElectrumClient;

namespace Liviano.Extensions
{
    public static class AccountExtensions
    {
        /// <summary>
        /// Helper function to allow the IAccount to be casted to e.g.: Bip141Account or any other account
        /// </summary>
        /// <param name="account">An <see cref="IAccount"/> that will be converted to something else</param>
        /// <returns></returns>
        public static IAccount CastToAccountType(this IAccount account)
        {
            return account.AccountType switch
            {
                "bip44" => (Bip44Account)account,
                "bip49" => (Bip49Account)account,
                "bip84" => (Bip84Account)account,
                "bip141" => (Bip141Account)account,
                "paper" => (PaperAccount)account,
                _ => throw new ArgumentException($"Invalid account type {account.AccountType}"),
            };
        }

        public static object TryGetProperty(this IAccount account, string name)
        {
            var prop = account.GetType().GetProperty(name);

            if (prop is null) return null;

            return prop.GetValue(account);
        }

        public static void TrySetProperty(this IAccount account, string name, object value)
        {
            var prop = account.GetType().GetProperty(name);

            if (prop is null) return;

            try
            {
                prop.SetValue(account, value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrySetProperty] Unable to set property of {name} on account, incorrect value {value}");
                Debug.WriteLine($"[TrySetProperty] Error: {ex.Message}");
            }
        }

        public static string GetZPrv(this IAccount acc)
        {
            return acc.ExtKey.ToZPrv();
        }

        public static string GetZPub(this IAccount acc)
        {
            return acc.ExtPubKey.ToZPub();
        }

        public static string GetYPrv(this IAccount acc)
        {
            return acc.ExtKey.ToYPrv();
        }

        public static string GetYPub(this IAccount acc)
        {
            return acc.ExtPubKey.ToYPub();
        }

        static Key[] GetCoinsKeys(this IAccount account, ICoin[] coins)
        {
            return coins.Select(o => account.GetCoinKey(o)).ToArray();
        }

        static Key GetCoinKey(this IAccount account, ICoin coin)
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

        public static ICoin[] GetCoinsFromTransaction(this IAccount account, Transaction tx)
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
                this IAccount account,
                Tx tx,
                decimal satsPerByte)
        {
            if (tx.IsReceive) throw new WalletException("[BumpFee] Bump fee failed because transaction was not sent by yourself");

            var builder = account.Network.CreateTransactionBuilder();
            var parentTransaction = Transaction.Parse(tx.Hex, account.Network);

            if (!parentTransaction.RBF) throw new WalletException("[BumpFee] Bump fee failed because transaction is not RBF");

            var coins = account.GetCoinsFromTransaction(parentTransaction);
            var keys = account.GetCoinsKeys(coins);

            builder.AddCoins(coins);
            builder.AddKeys(keys);

            if (parentTransaction.Outputs.Count == 1)
            {
                var output = parentTransaction.Outputs[0];
                var destinationAddress = output.ScriptPubKey.GetDestinationAddress(account.Network);
                var amount = output.Value;

                builder.Send(destinationAddress, amount);
                builder.SubtractFees();
            }
            else
            {
                foreach (var output in parentTransaction.Outputs.ToList())
                {
                    var destinationAddress = output.ScriptPubKey.GetDestinationAddress(account.Network);
                    var amount = output.Value;
                    bool isInternal = false;

                    foreach (var spkt in account.ScriptPubKeyTypes)
                        if (account.InternalAddresses[spkt].ToList().Any(o => o.Address.Equals(destinationAddress)))
                            isInternal = true;

                    if (isInternal)
                        builder.SetChange(destinationAddress);
                    else
                        builder.Send(destinationAddress, amount);
                }
            }

            builder.SetOptInRBF(true);
            builder.SendEstimatedFees(new FeeRate(satsPerByte));

            var childTransaction = builder.BuildTransaction(sign: true);
            var verified = VerifyTransaction(builder, childTransaction, out var errors);

            if (!verified)
            {
                string error = string.Join<string>(", ", errors.Select(o => o.Message));

                Debug.WriteLine($"[BumpFee] Error: {error}");

                throw new WalletException(error);
            }

            tx.ReplacedId = childTransaction.GetHash();

            return childTransaction;
        }

        public static (Transaction Transaction, string Error) CreateTransaction(
                this IAccount account,
                string destinationAddress,
                Money amount,
                decimal satsPerByte,
                bool rbf)
        {
            // Get coins from coin selector that satisfy our amount.
            // A typicalMinFee is a transaction sending to an address (bech32) spending only one input (bech32) in segwit inputs and outputs (bech32).
            var typicalMinFee = 141 * satsPerByte;
            var coinSelector = new DefaultCoinSelector();
            var unspentCoins = account.UnspentCoins.ToArray();

            var expectedCoins = coinSelector.Select(unspentCoins, amount + new Money(typicalMinFee, MoneyUnit.Satoshi));

            if (!expectedCoins.Any()) return (null, "Balance too low to create transaction.");

            var changeDestination = account.GetChangeAddress();
            var toDestination = BitcoinAddress.Create(destinationAddress, account.Network);
            var keys = account.GetCoinsKeys(unspentCoins);
            var builder = account.Network.CreateTransactionBuilder();

            // Build the tx
            builder.SetCoinSelector(coinSelector);
            builder.AddCoins(unspentCoins);
            builder.AddKeys(keys);
            builder.Send(toDestination, amount);
            builder.SetChange(changeDestination);
            builder.SetOptInRBF(rbf);
            builder.SendEstimatedFees(new FeeRate(satsPerByte));

            // Create transaction builder
            Transaction tx = null;
            try
            {
                tx = builder.BuildTransaction(sign: true);
            } catch (NotEnoughFundsException e)
            {
                var coinsStr = string.Join(", ", expectedCoins.Select((o) => $"{o.Outpoint.Hash}:{o.Amount}"));

                Debug.WriteLine($"[CreateTransaction] Error Message: {e.Message}");
                Debug.WriteLine($"[CreateTransaction] Coins: {coinsStr}");
                Debug.WriteLine($"[CreateTransaction] Destination: {toDestination}");
                Debug.WriteLine($"[CreateTransaction] Amount: {amount}");

                return (tx, e.Message);
            }

            var verified = VerifyTransaction(builder, tx, out var errors);

            if (!verified)
            {
                var errorMessage = string.Join<string>(", ", errors.Select(o => o.Message));

                Debug.WriteLine($"[CreateTransaction] Error: {errorMessage}");

                return (tx, errorMessage);
            }

            Debug.WriteLine($"[CreateTransaction] Tx: {tx.ToHex()}");
            foreach (var input in tx.Inputs)
                Debug.WriteLine($"[CreateTransaction] Inputs: {input.PrevOut.Hash}-{input.PrevOut.N}");

            return (tx, null);
        }

        static bool VerifyTransaction(
                TransactionBuilder builder,
                Transaction tx,
                out WalletException[] transactionPolicyErrors)
        {
            var verified = builder.Verify(tx, out var errors);
            var exceptions = new List<WalletException>();

            if (!verified)
                foreach (var error in errors)
                    exceptions.Add(new WalletException(error.ToString()));

            transactionPolicyErrors = exceptions.ToArray();

            return verified;
        }

        /// <summary>
        /// This will get all the transactions out to the total to calculate fees
        /// </summary>
        /// <param name="account">A <see cref="IAccount"/> to get the values from</param>
        /// <param name="inputs">A <see cref="TxInList"/> of the inputs from the tx</param>
        /// <returns>A <see cref="Money"/> with the outs value from N</returns>
        public static Money GetOutValueFromTxInputs(this IAccount account, TxInList inputs)
        {
            Money total = 0L;

            foreach (var input in inputs)
            {
                var outIndex = input.PrevOut.N;
                var outHash = input.PrevOut.Hash.ToString();
                var txIds = account.TxIds.ToList();
                var txs = account.Txs.ToList();

                // Try to find the tx locally to avoid performance issues
                var accountTxId = txIds.FirstOrDefault(o => o.Equals(outHash));
                if (!string.IsNullOrEmpty(accountTxId))
                {
                    var accountTx = txs.FirstOrDefault(o => o.Id.ToString().Equals(accountTxId));

                    var accountTransaction = Transaction.Parse(accountTx.Hex, account.Network);
                    var accountTxOut = accountTransaction.Outputs[outIndex];

                    total += accountTxOut.Value;

                    continue;
                }

                // Tx is not found locally we resource to the electrum pool to find it.
                // Get the transaction from the input
                var hex = GetTransactionHex(account, outHash);
                var transaction = Transaction.Parse(hex, account.Network);
                var txOut = transaction.Outputs[outIndex];

                total += txOut.Value;
            }

            return total;
        }

        static readonly ConcurrentDictionary<string, string> TxHexCache = new();
        static string GetTransactionHex(IAccount account, string outHash)
        {
            if (TxHexCache.ContainsKey(outHash)) return TxHexCache[outHash];

            Task<BlockchainTransactionGetResult> task;
            try
            {
                task = account.Wallet.ElectrumPool.ElectrumClient.BlockchainTransactionGet(outHash);
                task.Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[GetTransactionHex] Error: {}", ex);

                Task.Delay(250).Wait();

                return GetTransactionHex(account, outHash);
            }

            TxHexCache[outHash] = task.Result.Result;
            return TxHexCache[outHash];
        }

        public static Tx FindReplacedTx(this IAccount account, Transaction transaction)
        {
            Tx replacedTx = null;
            var unconfirmedRBFTransactions = account.Txs.Where(o => o.IsRBF && o.Confirmations == 0).ToList();

            foreach (var tx in unconfirmedRBFTransactions)
            {
                var parsedTransaction = Transaction.Parse(tx.Hex, tx.Network);

                if (transaction.Inputs.Count != parsedTransaction.Inputs.Count)
                    continue;

                bool sameInputs = true;
                for (int i = 0; i < parsedTransaction.Inputs.Count; i++)
                {
                    if (parsedTransaction.Inputs[i].PrevOut.Hash != transaction.Inputs[i].PrevOut.Hash || parsedTransaction.Inputs[i].PrevOut.N != transaction.Inputs[i].PrevOut.N)
                    {
                        sameInputs = false;

                        break;
                    }
                }

                if (sameInputs) replacedTx = tx;
            }

            return replacedTx;
        }

        public static bool IsReplacingTransaction(this IAccount account, Transaction transaction)
        {
            return account.FindReplacedTx(transaction) != null;
        }

        public static AccountInfo GetAccountInfo(this IAccount account)
        {
            return new AccountInfo()
            {
                Id = account.Id,
                Name = account.Name,
                HdPath = account.HdPath,
                Xpub = account.ExtPubKey.ToWif(),
                Xprv = account.ExtKey.ToWif(),
            };
        }
    }
}
