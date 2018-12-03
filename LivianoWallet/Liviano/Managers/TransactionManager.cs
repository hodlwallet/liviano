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
    class TransactionManager : ITransactionManager
    {
        IBroadcastManager _BroadcastManager;

        WalletManager _WalletManager;

        ICoinSelector _CoinSelector;

        ConcurrentChain _Chain;

        TransactionBuilder _Builder;

        private Coin[] GetCoins()
        {
            return _WalletManager.GetAllSpendableTransactions(CoinType.Bitcoin, _Chain.Height).Select(
                o => Transaction.Parse(o.Transaction.Hex, _WalletManager.Network)
            ).SelectMany(
                o => o.Outputs.AsCoins()
            ).ToArray();
        }

        public TransactionManager(IBroadcastManager broadcastManager, WalletManager walletManager, ICoinSelector coinSelector, ConcurrentChain chain)
        {
            _Chain = chain;
            _BroadcastManager = broadcastManager;
            _WalletManager = walletManager;
            _CoinSelector = coinSelector;
            _Builder = new TransactionBuilder();
        }

        public Transaction CreateTransaction(string destination, Money amount, int satoshisPerByte, HdAccount account, string password, bool signTransation = true)
        {
            Coin[] inputs = (Coin[]) _CoinSelector.Select(GetCoins(), amount).ToArray();
            HdAddress changeDestinationHdAddress = account.GetFirstUnusedChangeAddress();

            var toDestination = BitcoinAddress.Create(destination, _WalletManager.Network);
            var changeDestination = BitcoinAddress.Create(
                changeDestinationHdAddress.Address,
                _WalletManager.Network
            );

            List<Key> keys = new List<Key> ();

            foreach (Coin coin in inputs)
            {
                HdAddress coinAddress = account.InternalAddresses.First(o =>
                    o.Address == coin.ScriptPubKey.GetDestinationAddress(_WalletManager.Network).ToString()
                );
                
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
            Money fees = txWithNoFees.GetVirtualSize() / satoshisPerByte;

            if (inputs.Sum(o => o.Amount) < amount + fees)
            {
                throw new WalletException("This is a problem :(");
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

        public bool VerifyTranscation(Transaction tx , out WalletException[] transactionPolicyErrors)
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

        public async void BroadcastTransaction(Transaction transcationToBroadcast)
        {
           await _BroadcastManager.BroadcastTransactionAsync(transcationToBroadcast);
        }
    }
}
