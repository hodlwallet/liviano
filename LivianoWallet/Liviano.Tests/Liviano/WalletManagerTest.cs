using Xunit;

using NBitcoin;

using Liviano.Managers;
using Serilog;
using System.IO;

namespace Liviano.Tests.Liviano
{
    public class WalletManagerTest
    {
        ILogger _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        [Fact]
        public void CreateWalletWithoutPassword()
        {
            var wm = new WalletManager(_Logger, Network.Main);

            // Create data directory if it doesn't exists
            if (!Directory.Exists("data"))
                Directory.CreateDirectory("data");

            var mnemonic = wm.CreateWallet("Wallet Manager Test Wallet");

            Assert.NotNull(mnemonic);
            Assert.NotNull(wm.GetWallet());

            var wallet = wm.GetWallet();
        }
    }
}