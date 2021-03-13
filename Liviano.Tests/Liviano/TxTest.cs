using Xunit;

using Liviano.Models;

namespace Liviano.Tests.Liviano
{
    public class TxTest
    {
        [Fact]
        public void IsConfirmedWithTransactionHavingBlockHeightReturnsTrue()
        {
            var transaction = new Tx
            {
                Confirmations = 10
            };

            Assert.True(transaction.IsConfirmed());
        }

        [Fact]
        public void IsConfirmedWithTransactionHavingNoBlockHeightReturnsFalse()
        {
            var transaction = new Tx
            {
                Confirmations = 0
            };

            Assert.False(transaction.IsConfirmed());
        }
    }
}
