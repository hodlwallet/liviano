using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Liviano
{
    public interface IBroadcastManager
    {
        Task BroadcastTransactionAsync(Transaction transaction);


        TransactionBroadcastEntry GetTransaction(uint256 transactionHash);

        void AddOrUpdate(Transaction transaction, TransactionState state, string ErrorMessage = "");

    }
}
