using System;

namespace Liviano
{
    public class TransactionBroadcastEntry
    {
        public NBitcoin.Transaction Transaction { get; }

        public TransactionState State { get; set; }

        public string ErrorMessage { get; set; }

        public TransactionBroadcastEntry(NBitcoin.Transaction transaction, TransactionState state, string errorMessage)
        {
            this.Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            this.State = state;
            this.ErrorMessage = errorMessage;
        }
    }
}
