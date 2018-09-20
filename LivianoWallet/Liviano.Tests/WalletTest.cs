using System;
using Xunit;

namespace Liviano.Tests
{
    public class WalletTest
    {
        [Fact]
        public void TheTruth()
        {
            Assert.Equal(4, Add(2, 2));
        }

        [Fact]
        public void TheLies()
        {
            Assert.NotEqual(5, Add(2, 2));
        }

        int Add(int x, int y)
        {
            return x + y;
        }
    }
}
