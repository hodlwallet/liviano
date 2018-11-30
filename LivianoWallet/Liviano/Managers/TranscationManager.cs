using Liviano.Interfaces;
using Liviano.Models;
using NBitcoin;
using NBitcoin.Policy;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano.Managers
{
    class TranscationManager : ITranscationManager
    {
        IBroadcastManager _BroadcastManager;
        WalletManager _WalletManager;
        ICoinSelector _CoinSelector;
        ConcurrentChain _Chain;

        public TranscationManager(IBroadcastManager broadcastManager, WalletManager walletManager , ICoinSelector coinSelector, ConcurrentChain chain)
        {
            _Chain = chain;
            _BroadcastManager = broadcastManager;
            _WalletManager = walletManager;
            _CoinSelector = coinSelector;
        }

        public Transaction CreateTransaction(string desinationAddress, Money amount, int satoshisPerByte)
        {
            //_WalletManager.CreatAc
            //var coins = _WalletManager.GetAllSpendableTransactions(CoinType.Bitcoin,_Chain.Height)


            throw new NotImplementedException();
        }

        public Transaction SignTransaction(Transaction unsignedTransaction)
        {
            throw new NotImplementedException();
        }

        public bool VerifyTranscation(out TransactionPolicyError[] transactionPolicyErrors)
        {
            throw new NotImplementedException();
        }




    }
}
