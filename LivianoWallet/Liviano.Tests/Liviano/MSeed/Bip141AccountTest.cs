using Xunit;

using Liviano.MSeed;
using Liviano.MSeed.Accounts;

namespace Liviano.Tests.Liviano.MSeed
{

    public class Bip141AccountTest
    {
        const string MNEMONIC = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Fact]
        public void TestAddresses()
        {
            var account = GetAccount();

            var address = account.GetReceivingAddress();

            Assert.NotNull(address);

            Assert.Equal("bc1qgv52mt89gpev6p56huggl970sppqkgftxakv7f", address.ToString());
        }

        Bip141Account GetAccount()
        {
            var w = GetWallet();

            w.AddAccount("bip141");

            return (Bip141Account)w.Accounts[0];
        }

        Wallet GetWallet()
        {
            var w = new Wallet();
            w.Init(MNEMONIC);

            return w;
        }
    }
}