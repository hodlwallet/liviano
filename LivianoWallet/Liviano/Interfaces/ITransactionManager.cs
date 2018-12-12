using System;
using System.Collections.Generic;
using System.Text;

using NBitcoin;

using Liviano.Exceptions;
using Liviano.Models;
using System.Threading.Tasks;

namespace Liviano.Interfaces
{
    public interface ITransactionManager
    {
        Transaction CreateTransaction(string destination, Money amount, int satoshisPerByte, HdAccount account, string password, bool signTransaction = true);

        bool VerifyTransaction(Transaction transaction, out WalletException[] transactionPolicyErrors);

        Transaction SignTransaction(Transaction unsignedTransaction);

        Task BroadcastTransaction(Transaction transactionToBroadcast);
    }
}
