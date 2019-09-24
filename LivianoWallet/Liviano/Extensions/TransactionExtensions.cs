using System;

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

		public static Transaction CreateTransaction(string destination, Money amount, long satsPerByte, IAccount account, Network network, string password = "")
		{
			var coinSelector = new DefaultCoinSelector();
			ICoin[] inputs = coinSelector.Select(GetSpendableCoins(account, network), amount).ToArray();

			if (coinSelector == null)
			{
				throw new WalletException("Balance too low to craete transaction.");
			}

			var changeDestinationAddress = account.GetChangeAddress();
			var toDestination = BitcoinAddress.Create(destination, network);


		}
	}
}
