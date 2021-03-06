//
// WalletTest.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
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
// THE SOFTWARE.
using Xunit;

using NBitcoin;
using NBitcoin.JsonConverters;

using Newtonsoft.Json;

namespace Liviano.Tests.Funcional
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

            var settings = new JsonSerializerSettings();
            Serializer.RegisterFrontConverters(settings, Network.Main);

            var json = JsonConvert.SerializeObject(w, settings: settings);
            Assert.Contains($"\"id\":\"{w.Id}\"", json);
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
        public void TestAddBip84Account()
        {
            var w = new Wallet();

            w.Init(MNEMONIC);

            w.AddAccount("bip84", "New Segwit Account");

            Assert.NotEmpty(w.Accounts);

            var a = w.Accounts[0];

            Assert.NotNull(a);
            Assert.NotEmpty(a.Id);

            var addr = a.GetReceiveAddress();

            Assert.Equal("bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu", addr.ToString());
        }

        [Fact]
        public void TestAddBip84AccountTestnet()
        {
            var w = new Wallet();

            w.Init(MNEMONIC, network: Network.TestNet);

            w.AddAccount("bip84", "New Segwit Account");

            Assert.NotEmpty(w.Accounts);

            var a = w.Accounts[0];

            Assert.NotNull(a);
            Assert.NotEmpty(a.Id);

            var addr = a.GetReceiveAddress();

            Assert.Equal("tb1q6rz28mcfaxtmd6v789l9rrlrusdprr9pqcpvkl", addr.ToString());
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
    }
}
