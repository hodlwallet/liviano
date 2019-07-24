using Liviano.Exceptions;
using Liviano.Interfaces;
using Liviano.Models;
using NBitcoin;
using NBitcoin.Policy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liviano.Managers
{
    // TODO Transaction manager does not have a log... sucks.
    public class TransactionManager : ITransactionManager
    {
        IBroadcastManager _BroadcastManager;

        WalletManager _WalletManager;

        ICoinSelector _CoinSelector;

        ConcurrentChain _Chain;

        private Coin[] GetCoins(HdAccount account)
        {
            var results = _WalletManager.GetAllSpendableTransactions(CoinType.Bitcoin, _Chain.Height)
            .Where(
                o => o.Account.Index == account.Index
            )
            .Select(
                o => Transaction.Parse(o.Transaction.Hex, _WalletManager.Network)
            )
            .SelectMany(
                o => o.Outputs.AsCoins()
            )
            .ToArray();

            return results;
        }

        public TransactionManager(IBroadcastManager broadcastManager, WalletManager walletManager, ICoinSelector coinSelector, ConcurrentChain chain)
        {
            _Chain = chain;
            _BroadcastManager = broadcastManager;
            _WalletManager = walletManager;
            _CoinSelector = coinSelector;
        }

        public Transaction CreateTransaction(string destination, Money amount, long satoshisPerKB, HdAccount account, string password = "", bool signTransation = true)
        {
            // Get coins from coin selector that satisfy our amount
            ICoin[] coins = _CoinSelector.Select(GetCoins(account), amount).ToArray();

            if (coins == null)
            {
                throw new WalletException("Balance too low to create transaction");
            }

            // Get addresses to send (destination) and change
            HdAddress changeDestinationHdAddress = account.GetFirstUnusedChangeAddress();

            var toDestination = BitcoinAddress.Create(destination, _WalletManager.Network);
            var changeDestination = BitcoinAddress.Create(changeDestinationHdAddress.Address, _WalletManager.Network);

            // Populate the signing keys of each coin
            Key[] keys = GetCoinKeys(account, coins, password);

            var noFeeBuilder = _WalletManager.Network.CreateTransactionBuilder();
            // Create transaction builder with change and signing keys
            Transaction txWithNoFees = noFeeBuilder
                .AddCoins(coins)
                .AddKeys(keys.ToArray())
                .Send(toDestination, amount)
                .SetChange(changeDestination)
                .BuildTransaction(sign: signTransation);

            // Calculate fees
            Money fees = txWithNoFees.GetVirtualSize() * (satoshisPerKB / 1000);

            // If fees are enough with the coins we got, we should just create the tx.
            if (coins.Sum(o => o.TxOut.Value) >= (fees + amount))
            {
                var goodEnoughBuilder = _WalletManager.Network.CreateTransactionBuilder();
                return goodEnoughBuilder
                    .AddCoins(coins)
                    .AddKeys(keys.ToArray())
                    .Send(toDestination, amount)
                    .SendFees(fees)
                    .SetChange(changeDestination)
                    .BuildTransaction(sign: signTransation);
            }

            // If the coins do not satisfy the fees + amount grand total, then we repeat the process
            coins = _CoinSelector.Select(GetCoins(account), amount + fees).ToArray();

            // Coins will be empty if the tx cannot be created by the coin selector
            if (coins == null)
            {
                throw new WalletException("Balance too low to create transaction");
            }

            // Get the keys for the coins again...
            keys = GetCoinKeys(account, coins, password);

            var finalBuilder = _WalletManager.Network.CreateTransactionBuilder();
            // Finally send the transcation
            return finalBuilder
                .AddCoins(coins)
                .AddKeys(keys.ToArray())
                .Send(toDestination, amount)
                .SendFees(fees)
                .SetChange(changeDestination)
                .BuildTransaction(sign: signTransation);
        }

        public Transaction SignTransaction(Transaction unsignedTransaction)
        {
            var builder = _WalletManager.Network.CreateTransactionBuilder();
            return builder.SignTransaction(unsignedTransaction);
        }

        public bool VerifyTransaction(Transaction tx , out WalletException[] transactionPolicyErrors)
        {
            var builder = _WalletManager.Network.CreateTransactionBuilder();
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

        public async Task BroadcastTransaction(Transaction transactionToBroadcast)
        {
           await _BroadcastManager.BroadcastTransactionAsync(transactionToBroadcast);
        }

        Key[] GetCoinKeys(HdAccount account, ICoin[] coins, string password = "")
        {
            List<Key> keys = new List<Key>();
            foreach (Coin coin in coins)
            {
                HdAddress coinAddress;
                try
                {
                    coinAddress = account.ExternalAddresses.Concat(account.InternalAddresses).First(
                        o => o.Transactions.Any(u => u.Id == coin.Outpoint.Hash)
                    );
                }
                catch (InvalidOperationException e)
                {
                    throw new WalletException(e.Message);
                }

                keys.Add(
                    _WalletManager.Wallet.GetExtendedPrivateKeyForAddress(coinAddress, password).PrivateKey
                );
            }

            return keys.ToArray();
        }
    }
}
