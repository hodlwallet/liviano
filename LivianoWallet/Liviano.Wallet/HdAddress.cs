using System;
using System.Collections.Generic;
using NBitcoin;

namespace Liviano
{
    public class HdAddress
    {
        public HdAddress()
        {
            this.Transactions = new List<TransactionData>();
        }

        public ICollection<TransactionData> Transactions { get; set; }
    }
}
