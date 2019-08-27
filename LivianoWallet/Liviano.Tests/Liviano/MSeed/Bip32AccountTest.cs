using Xunit;

using NBitcoin;

using Liviano.MSeed;
using Liviano.MSeed.Accounts;

namespace Liviano.Tests.Liviano.MSeed
{
    public class Bip32AccountTest
    {
        const string MNEMONIC = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Fact]
        public void TestAddresses()
        {
            var account = GetAccount();

            var address = account.GetReceiveAddress();

            Assert.NotNull(address);

            Assert.Equal("bc1qgv52mt89gpev6p56huggl970sppqkgftxakv7f", address.ToString());

            // Reset account count to test legacy address generation
            account.ExternalAddressesCount = 0;
            account.ScriptPubKeyType = ScriptPubKeyType.Legacy;

            address = account.GetReceiveAddress();

            Assert.NotNull(address);

            Assert.Equal("17871ErDqdevLTLWBH6WzjUc1EKGDQzCMA", address.ToString());

            account.ScriptPubKeyType = ScriptPubKeyType.Segwit;

            address = account.GetChangeAddress();

            Assert.NotNull(account);
            Assert.Equal("bc1qunmfkdmckn76c8nmf3g22du699gne5q8c3xqhl", address.ToString());

            // generate legacy change address for some reason... should never be called
            account.InternalAddressesCount = 0;
            account.ScriptPubKeyType = ScriptPubKeyType.Legacy;

            address = account.GetChangeAddress();

            Assert.NotNull(address);
            Assert.Equal("1MseVFBWLkbPeGMpkAsahBujinBq3QjGo4", address.ToString());
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