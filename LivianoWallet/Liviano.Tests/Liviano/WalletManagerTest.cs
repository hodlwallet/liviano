using Xunit;

using NBitcoin;

using Liviano.Managers;
using Serilog;
using System.IO;
using System;
using Xunit.Sdk;
using System.Reflection;

namespace Liviano.Tests.Liviano
{
    public class BeforeWalletManagerTest : BeforeAfterTestAttribute
    {
        ILogger _Logger;

        public BeforeWalletManagerTest()
        {
            _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
        }

        public override void Before(MethodInfo methodUnderTest)
        {
            try
            {
                Directory.CreateDirectory("data");
            }
            catch
            {
                _Logger.Error("Cannot create 'data' directory before test");
            }
        }

        public override void After(MethodInfo methodUnderTest)
        {
            try
            {
                Directory.Delete("data", recursive: true);
            }
            catch
            {
                _Logger.Error("Cannot delete 'data' directory after running test");
            }
        }
    }

    public class WalletManagerTest
    {
        ILogger _Logger;

        public WalletManagerTest()
        {
            // Setup log
            // Clear a file
            System.IO.File.WriteAllText("test.log", string.Empty);
            _Logger = new LoggerConfiguration().WriteTo.File("test.log", rollingInterval: RollingInterval.Day).CreateLogger();
        }

        [Fact]
        [BeforeWalletManagerTest]
        public void CreateWalletWithoutPassword()
        {
            var wm = new WalletManager(_Logger, Network.Main, "wmtest");

            var mnemonic = wm.CreateWallet("wmtest");

            Assert.NotNull(mnemonic);
            Assert.NotNull(wm.Wallet);

            var wallet = wm.Wallet;
        }

        [Fact]
        [BeforeWalletManagerTest]
        public void CreateWalletWithPassword()
        {
            var wm = new WalletManager(_Logger, Network.Main, "wmtest");

            var mnemonic = wm.CreateWallet("wmtest", "test");

            Assert.NotNull(mnemonic);
            Assert.NotNull(wm.Wallet);

            var wallet = wm.Wallet;

            Assert.StartsWith("", wallet.EncryptedSeed);
        }
    }
}