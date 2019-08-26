using System.Diagnostics;
using System;
using Xunit;

using NBitcoin;

using Newtonsoft.Json;

using Liviano.MSeed;

namespace Liviano.Tests.Liviano.MSeed
{
    public class WalletTest
    {
        [Fact]
        public void TestWalletInit()
        {
            var w = new Wallet()
            {
                Name = "Testy Wallet"
            };

            w.Init();

            Assert.Equal(Network.Main, w.Network);
            Assert.Equal("Testy Wallet", w.Name);
            Assert.NotNull(w.Id);

            Assert.True(JsonConvert.SerializeObject(w).Contains($"\"id\":\"{w.Id}\""));

            Console.WriteLine(JsonConvert.SerializeObject(w));

            var expectedAccountTypes = string.Empty;
            foreach(var at in w.AccountTypes)
                expectedAccountTypes += $"\"{at}\",";
            expectedAccountTypes = expectedAccountTypes.Remove(expectedAccountTypes.Length - 1);

            Console.WriteLine(expectedAccountTypes);

            Assert.True(JsonConvert.SerializeObject(w).Contains($"\"accountTypes\":[{expectedAccountTypes}]"));
        }
    }
}