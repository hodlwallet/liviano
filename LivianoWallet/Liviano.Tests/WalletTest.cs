using System;
using Xunit;
using NBitcoin;

namespace Liviano.Tests
{
    public class WalletTest
    {
        [Fact]
        public void InitTest()
        {
            Wallet w = new Wallet(
                name: "1st Wallet",
                creationTime: new DateTimeOffset(DateTime.Parse("2018-10-12"))
            );

            Assert.Equal("1st Wallet", w.Name);
            Assert.Equal(Network.Main, w.Network);

            Assert.Empty(w.AccountsRoot);
            Assert.Empty(w.BlockLocator);
            Assert.Empty(w.ChainCode);
            Assert.Empty(w.EncryptedSeed);

            Assert.True(w.IsExtPubKeyWallet);
            Assert.Equal(w.CreationTime, new DateTimeOffset(DateTime.Parse("2018-10-12")));
        }

        [Fact]
        public void IsMaximilist()
        {
            Assert.True(CoinType.Bitcoin == 0);
        }
    }
}
