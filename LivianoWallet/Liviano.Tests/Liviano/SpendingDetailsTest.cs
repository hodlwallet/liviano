using Liviano.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Liviano.Tests.Liviano
{
   public class SpendingDetailsTest
    {
        [Fact]
        public void IsSpentConfirmedHavingBlockHeightReturnsTrue()
        {
            var spendingDetails = new SpendingDetails
            {
                BlockHeight = 15
            };
            Assert.True(spendingDetails.IsSpentConfirmed());
        }

        [Fact]
        public void IsConfirmedHavingNoBlockHeightReturnsFalse()
        {
            var spendingDetails = new SpendingDetails
            {
                BlockHeight = null
            };
            Assert.False(spendingDetails.IsSpentConfirmed());
        }
   }
}
