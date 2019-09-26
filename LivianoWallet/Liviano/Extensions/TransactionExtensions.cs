// TODO: Add License
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using NBitcoin;

using Liviano.Interfaces;
using Liviano.Exceptions;
using Liviano.Utilities;

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

        public static Transaction CreateTransaction(string password, string destinationAddress, Money amount, long satsPerByte, Wallet wallet, IAccount account, Network network)
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

        public static async Task BroadcastTransaction(Transaction transactionToBroadcast)
        {
            Guard.NotNull(transactionToBroadcast, nameof(transactionToBroadcast));
        }
    }
}