using NBitcoin;
using NBitcoin.Policy;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano.Interfaces
{
    interface ITranscationManager
    {
        Transaction CreateTransaction(string desinationAddress, Money amount, int satoshisPerByte );

        bool VerifyTranscation(out TransactionPolicyError[] transactionPolicyErrors);

        Transaction SignTransaction(Transaction unsignedTransaction);
    }
}
