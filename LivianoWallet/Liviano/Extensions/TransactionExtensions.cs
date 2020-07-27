//
// TransactionExtensions.cs
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
using System.Linq;
using System.Collections.Generic;

using NBitcoin;

using Liviano.Interfaces;
using Liviano.Exceptions;

namespace Liviano.Extensions
{
    public static class TransactionExtensions
    {
        public static Coin[] GetSpendableCoins(IAccount account, Network network)
        {
            var results = account.Txs
            .Where(
                o => o.IsSpendable() == true
            )
            .Select(
                o => Transaction.Parse(o.Hex, network)
            )
            .SelectMany(
                o => o.Outputs.AsCoins()
            )
            .ToArray();

            return results;
        }

        public static Transaction CreateTransaction(string password, string destinationAddress, Money amount, long satsPerByte, IWallet wallet, IAccount account, Network network)
        {
            // Get coins from coin selector that satisfy our amount.
            var coinSelector = new DefaultCoinSelector();
            ICoin[] coins = coinSelector.Select(GetSpendableCoins(account, network), amount).ToArray();

            if (coins == null)
            {
                throw new WalletException("Balance too low to craete transaction.");
            }

            var changeDestination = account.GetChangeAddress();
            var toDestination = BitcoinAddress.Create(destinationAddress, network);

            var noFeeBuilder = network.CreateTransactionBuilder();
            // Create transaction buidler with change and signing keys.
            Transaction txWithNoFees = noFeeBuilder
                .AddCoins(coins)
                .AddKeys(wallet.GetPrivateKey(password, true))
                .Send(toDestination, amount)
                .SetChange(changeDestination)
                .BuildTransaction(sign: true);

            // Calculate fees
            Money fees = txWithNoFees.GetVirtualSize() * satsPerByte;

            // If fees are enough with the coins we got, we should just create the tx.
            if (coins.Sum(o => o.TxOut.Value) >= (fees + amount))
            {
                var goodEnoughBuilder = network.CreateTransactionBuilder();
                return goodEnoughBuilder
                    .AddCoins(coins)
                    .AddKeys(wallet.GetPrivateKey(password, true))
                    .Send(toDestination, amount)
                    .SendFees(fees)
                    .SetChange(changeDestination)
                    .BuildTransaction(sign: true);
            }

            // If the coins do not satisfy the fees + amount grand total, then we repeat the process.
            coins = coinSelector.Select(GetSpendableCoins(account, network), amount + fees).ToArray();

            if (coins == null)
            {
                throw new WalletException("Balance too low to create transaction");
            }

            var finalBuilder = network.CreateTransactionBuilder();
            // Finally send the transaction.
            return finalBuilder
                .AddCoins(coins)
                .AddKeys(wallet.GetPrivateKey(password, true))
                .Send(toDestination, amount)
                .SendFees(fees)
                .SetChange(changeDestination)
                .BuildTransaction(sign: true);
        }

        public static bool VerifyTransaction(Transaction tx, Network network, out WalletException[] transactionPolicyErrors)
        {
            var builder = network.CreateTransactionBuilder();
            var flag = builder.Verify(tx, out var errors);
            var exceptions = new List<WalletException>();

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    exceptions.Add(new WalletException(error.ToString()));
                }
            }

            transactionPolicyErrors = exceptions.ToArray();

            return flag;
        }
    }
}
