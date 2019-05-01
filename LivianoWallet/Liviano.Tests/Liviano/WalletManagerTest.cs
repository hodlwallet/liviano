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
        public override void Before(MethodInfo methodUnderTest)
        {
            try
            {
                Directory.CreateDirectory("data");
            }
            catch
            {
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

        [Fact (Skip = "Too slow to run, this takes 40 seconds...")]
        [BeforeWalletManagerTest]
        public void CreateWalletWithoutPassword()
        {
            var wm = new WalletManager(_Logger, Network.Main, "wmtest");

            var mnemonic = wm.CreateWallet("wmtest");

            Assert.NotNull(mnemonic);
            Assert.NotNull(wm.GetWallet());

            var wallet = wm.GetWallet();
        }
    }
}