using System;
using Xunit;
using NBitcoin;

namespace Liviano.Tests.Liviano
{
    public class HdOperationsTest
    {
        [Fact]
        public void GetNetworkTest()
        {
            Assert.Equal(Network.Main, HdOperations.GetNetwork());
            Assert.Equal(Network.Main, HdOperations.GetNetwork("main"));
            Assert.Equal(Network.TestNet, HdOperations.GetNetwork("testnet"));
            Assert.Equal(Network.RegTest, HdOperations.GetNetwork("regtest"));

            Assert.Throws<WalletException>(() => HdOperations.GetNetwork("Liviano"));
        }

        [Fact]
        public void CreateHdPathTest()
        {
            Assert.Equal(
                "m/44'/0'/0'/0/0",
                HdOperations.CreateHdPath(
                    0 /* Bitcoin */,
                    0,
                    false,
                    0
                )
            );

            Assert.Equal(
                "m/44'/0'/0'/1/0",
                HdOperations.CreateHdPath(
                    0 /* Bitcoin */,
                    0,
                    true,
                    0
                )
            );
        }
    }
}