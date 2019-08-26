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
        const string MNEMONIC = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Fact]
        public void TestWalletInit()
        {
            var w = new Wallet()
            {
                Name = "Testy Wallet"
            };

            w.Init(MNEMONIC);

            Assert.Equal(Network.Main, w.Network);
            Assert.Equal("Testy Wallet", w.Name);
            Assert.NotNull(w.Id);

            Assert.Contains($"\"id\":\"{w.Id}\"", JsonConvert.SerializeObject(w));
            Assert.Contains($"\"accountTypes\":[{GetExpectedAccountTypesStr(w)}]", JsonConvert.SerializeObject(w));
        }

        [Fact]
        public void TestAddAccount()
        {
            var w = new Wallet();

            w.Init(MNEMONIC);

            w.AddAccount("bip141", "Old Hodl Account");

            Assert.NotEmpty(w.Accounts);

            var a = w.Accounts[0];

            Assert.NotNull(a);
            Assert.NotEmpty(a.Id);
        }

        string GetExpectedAccountTypesStr(Wallet wallet)
        {
            var expectedAccountTypes = string.Empty;
            foreach(var at in wallet.AccountTypes)
                expectedAccountTypes += $"\"{at}\",";
            expectedAccountTypes = expectedAccountTypes.Remove(expectedAccountTypes.Length - 1);

            return expectedAccountTypes;
        }
    }
}