using Liviano.Exceptions;
using Liviano.Interfaces;
using Liviano.Models;
using NBitcoin;
using NBitcoin.Policy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Liviano.Managers
{
    public class TransactionManager : ITransactionManager
    {
        IBroadcastManager _BroadcastManager;

        WalletManager _WalletManager;

        ICoinSelector _CoinSelector;

        ConcurrentChain _Chain;

        TransactionBuilder _Builder;

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
            _Builder = _WalletManager.Network.CreateTransactionBuilder();
        }

        public Transaction CreateTransaction(string destination, Money amount, int satoshisPerByte, HdAccount account, string password, bool signTransation = true)
        {
            IEnumerable<ICoin> inputs = _CoinSelector.Select(GetCoins(account), amount);

            if (inputs == null)
            {
                throw new WalletException("Balance too low to create transaction");
            }

            HdAddress changeDestinationHdAddress = account.GetFirstUnusedChangeAddress();

            var toDestination = BitcoinAddress.Create(destination, _WalletManager.Network);
            var changeDestination = BitcoinAddress.Create(changeDestinationHdAddress.Address, _WalletManager.Network);

            List<Key> keys = new List<Key>();

            foreach (Coin coin in inputs)
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
                    _WalletManager.GetWallet().GetExtendedPrivateKeyForAddress(password, coinAddress).PrivateKey
                );
            }

            Transaction txWithNoFees = _Builder
                .AddCoins(inputs)
                .AddKeys(keys.ToArray())
                .Send(toDestination, amount)
                .SetChange(changeDestination)
                .BuildTransaction(sign: signTransation);

            // Calculate fees
            Money fees = satoshisPerByte * txWithNoFees.GetVirtualSize();

            inputs = _CoinSelector.Select(GetCoins(account), amount + fees);

            if (inputs == null)
            {
                throw new WalletException("Balance too low to create transaction");
            }

            keys.Clear();

            foreach (Coin coin in inputs)
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
                    _WalletManager.GetWallet().GetExtendedPrivateKeyForAddress(password, coinAddress).PrivateKey
                );
            }

            return _Builder
                .AddCoins(inputs)
                .AddKeys(keys.ToArray())
                .Send(toDestination, amount)
                .SendFees(fees)
                .SetChange(changeDestination)
                .BuildTransaction(sign: signTransation);
        }

        public Transaction SignTransaction(Transaction unsignedTransaction, Coin[] coins, Key[] keys)
        {
            if (unsignedTransaction.Inputs.All(i => i.GetSigner() != null))
            {
                throw new WalletException("Transaction already signed");
            }

            unsignedTransaction.Sign(keys, coins);

            return unsignedTransaction;
        }

        public bool VerifyTransaction(Transaction tx , out WalletException[] transactionPolicyErrors)
        {
            var flag = _Builder.Verify(tx, out var errors);
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

        public async void BroadcastTransaction(Transaction transactionToBroadcast)
        {
           await _BroadcastManager.BroadcastTransactionAsync(transactionToBroadcast);
        }
    }
}
