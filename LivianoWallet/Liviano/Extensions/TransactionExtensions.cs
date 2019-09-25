// TODO: Add License
using System;
using System.Linq;

using NBitcoin;

using Liviano.Interfaces;
using Liviano.Exceptions;

namespace Liviano.Extensions
{
    public class TransactionExtensions
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

		public static Transaction CreateTransaction(string password, string destinationAddress, Money amount, long satsPerKB, Wallet wallet, IAccount account, Network network)
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
            // Create transaction buidler with change and signing keys
            Transaction txWithNoFees = noFeeBuilder
                .AddCoins(coins)
                .AddKeys(wallet.GetExtendedKey())
                .Send(toDestination, amount)
                .SetChange(changeDestination)
                .BuildTransaction(sign: true);

            //Calculate fees
            Money fees = txWithNoFees.GetVirtualSize() * (satsPerKB / 1000);
		}
	}
}
