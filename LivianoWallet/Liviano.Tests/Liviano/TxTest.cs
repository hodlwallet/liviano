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
                BlockHeight = 15
            };

            Assert.True(transaction.IsConfirmed());
        }

        [Fact]
        public void IsConfirmedWithTransactionHavingNoBlockHeightReturnsFalse()
        {
            var transaction = new Tx
            {
                BlockHeight = null
            };

            Assert.False(transaction.IsConfirmed());
        }
    }
}
