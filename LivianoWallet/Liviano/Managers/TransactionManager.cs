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
            _Builder = _WalletManager.Network.CreateTransactionBuilder();
        }

        public Transaction CreateTransaction(string destination, Money amount, int satoshisPerByte, HdAccount account, string password)
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
                .BuildTransaction(sign: false);

            // Calculate fees
            Money fees = txWithNoFees.GetVirtualSize() / satoshisPerByte;

            return _Builder
                .AddCoins(inputs)
                .AddKeys(keys.ToArray())
                .Send(toDestination, amount)
                .SendFees(fees)
                .SetChange(changeDestination)
                .BuildTransaction(sign: false);
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

        public bool VerifyTranscation(Transaction tx/*, out TransactionPolicyError[] transactionPolicyErrors*/)
        {
            // transactionPolicyErrors.Append(new TransactionPolicyError());
            return _Builder.Verify(tx);
        }
    }
}
