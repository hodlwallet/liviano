using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano.Models
{
    /// <summary>
    /// A class that represents the balance of an account.
    /// </summary>
    public class AccountBalance
    {
        /// <summary>
        /// The account for which the balance is calculated.
        /// </summary>
        public HdAccount Account { get; set; }

        /// <summary>
        /// The balance of confirmed transactions.
        /// </summary>
        public Money AmountConfirmed { get; set; }

        /// <summary>
        /// The balance of unconfirmed transactions.
        /// </summary>
        public Money AmountUnconfirmed { get; set; }
    }
}
