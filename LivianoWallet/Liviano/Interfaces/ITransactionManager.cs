using System;
using System.Collections.Generic;
using System.Text;

using NBitcoin;

using Liviano.Exceptions;
using Liviano.Models;

namespace Liviano.Interfaces
{
    interface ITransactionManager
    {
        Transaction CreateTransaction(string destination, Money amount, int satoshisPerByte, HdAccount account, string password, bool signTransaction = true);

        bool VerifyTranscation(Transaction transaction, out WalletException[] transactionPolicyErrors);

        Transaction SignTransaction(Transaction unsignedTransaction, Coin[] coins, Key[] keys);
    }
}
