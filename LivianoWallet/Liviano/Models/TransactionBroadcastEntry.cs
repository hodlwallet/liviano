using Liviano.Enums;
using System;

namespace Liviano.Models
{
    public class TransactionBroadcastEntry
    {
        public NBitcoin.Transaction Transaction { get; }

        public TransactionState State { get; set; }

        public string ErrorMessage { get; set; }

        public TransactionBroadcastEntry(NBitcoin.Transaction transaction, TransactionState state, string errorMessage)
        {
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            State = state;
            ErrorMessage = errorMessage;
        }
    }
}
