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

namespace Liviano.Extensions
{
    public static class TransactionExtensions
    {
        public static Transaction CreateTransaction(string password, string destinationAddress, Money amount, long satsPerByte, IWallet wallet, IAccount account)
        {
            // Get coins from coin selector that satisfy our amount.
            var coinSelector = new DefaultCoinSelector();
            ICoin[] coins = coinSelector.Select(account.GetSpendableCoins(), amount).ToArray();

            if (coins == null)
            {
                throw new WalletException("Balance too low to create transaction.");
            }

            var changeDestination = account.GetChangeAddress();
            var toDestination = BitcoinAddress.Create(destinationAddress, account.Network);

            var builder = account.Network.CreateTransactionBuilder();

            // Create transaction buidler with change and signing keys.
            var tx = builder
                .AddKeys(wallet.GetPrivateKey(password, true))
                .AddCoins(coins)
                .Send(toDestination, amount)
                .SetChange(changeDestination)
                .SendEstimatedFees(new FeeRate(satsPerByte))
                .BuildTransaction(sign: true);

            Debug.WriteLine($"[CreateTransaction] Tx: {tx.ToHex()}");

            return tx;
        }

        public static bool VerifyTransaction(IAccount account, Transaction tx, out WalletException[] transactionPolicyErrors)
        {
            var builder = account.Network.CreateTransactionBuilder();
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
