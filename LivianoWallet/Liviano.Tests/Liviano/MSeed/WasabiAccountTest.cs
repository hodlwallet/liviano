//
// WasabiAccountTest.cs
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
// THE SOFTWARE.
using Xunit;

using NBitcoin;

using Liviano.MSeed;
using Liviano.MSeed.Accounts;

namespace Liviano.Tests.Liviano.MSeed
{
    public class WasabiAccountTest
    {
        const string MNEMONIC = "uphold universe great security drink equal address fossil spice ready foil able";
        const string WASABI_MNEMONIC = "uphold universe great security drink equal address fossil spice ready foil able";
        const string WASABI_PASSWORD = "";

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

        WasabiAccount GetAccount()
        {
            var w = GetWallet();

            w.AddAccount("wasabi");

            return (WasabiAccount)w.Accounts[0];
        }

        Wallet GetWallet()
        {
            var w = new Wallet();
            w.Init(MNEMONIC);

            return w;
        }
    }
}
