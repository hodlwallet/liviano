using Liviano.Models;
using NBitcoin;
using NBitcoin.Policy;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano.Interfaces
{
    interface ITransactionManager
    {
        Transaction CreateTransaction(string destination, Money amount, int satoshisPerByte, HdAccount account, string password);

        bool VerifyTranscation(Transaction transaction/*, out TransactionPolicyError[] transactionPolicyErrors*/);

        Transaction SignTransaction(Transaction unsignedTransaction, Coin[] coins, Key[] keys);
    }
}
