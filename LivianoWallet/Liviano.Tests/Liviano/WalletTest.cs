//
// WalletTest.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.using Xunit;
using Xunit;

using NBitcoin;

using Newtonsoft.Json;

namespace Liviano.Tests.Liviano
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
        public void TestAddBip141Account()
        {
            var w = new Wallet();

            w.Init(MNEMONIC);

            w.AddAccount("bip141", "Old Hodl Account");

            Assert.NotEmpty(w.Accounts);

            var a = w.Accounts[0];

            Assert.NotNull(a);
            Assert.NotEmpty(a.Id);
        }

        [Fact]
        public void TestAddPaperAccount()
        {
            var w = new Wallet();

            w.Init(MNEMONIC);

            w.AddAccount("paper", "My Paper Wallet", new { w.Network });

            Assert.NotEmpty(w.Accounts);

            var a = w.Accounts[0];

            Assert.NotNull(a);
            Assert.NotEmpty(a.Id);
        }

        string GetExpectedAccountTypesStr(Wallet wallet)
        {
            var expectedAccountTypes = string.Empty;
            foreach (var at in wallet.AccountTypes)
                expectedAccountTypes += $"\"{at}\",";
            expectedAccountTypes = expectedAccountTypes.Remove(expectedAccountTypes.Length - 1);

            return expectedAccountTypes;
        }
    }
}