//
// PaperAccountTest.cs
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
using System;

using Xunit;

using NBitcoin;

using Liviano.Accounts;

namespace Liviano.Tests.Funcional
{
    public class PaperAccountTest
    {
        const string MNEMONIC = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        Wallet wallet;

        [Fact]
        public void TestDifferentPrivateKeysInWallet()
        {
            var w = GetWallet();
            var a = GetAccount(ScriptPubKeyType.Segwit);

            Assert.NotEqual(w.GetPrivateKey(), a.PrivateKey);
        }

        [Fact]
        public void TestCannotGenerateChangeAddress()
        {
            var a = GetAccount(ScriptPubKeyType.Legacy);

            Assert.Throws<ArgumentException>(() => a.GetChangeAddress());
        }

        [Fact]
        public void TestAddress()
        {
            var account = GetAccount(ScriptPubKeyType.SegwitP2SH, "KyXW8nD6SgQgVfwQFhhCKhHvJ79pCyWGsYgoAPz2DT7zbdKfEVBs");

            var address = account.GetReceiveAddress();

            Assert.Equal("3Ee2GJq8rLd1LodKY1XfDGnQnJ73GV4k8r", address.ToString());
        }

        [Fact]
        public void TestBitAddress()
        {
            var account = GetAccount(ScriptPubKeyType.Legacy, "L475uGVDmBAu1389Ey4EscdnGTa9HFozBB5GdpHXHR4WkSfocy4M");

            var address = account.GetReceiveAddress();

            Assert.Equal("19Vydw6ChsuCgptKSYro4Gc2z1h7mYKbXp", address.ToString());
        }

        PaperAccount GetAccount(ScriptPubKeyType scriptPubKeyType, string wif = null)
        {
            var w = GetWallet();

            if (wif is null)
                w.AddAccount("paper", "My Paper Wallet", new { ScriptPubKeyType = scriptPubKeyType, w.Network });
            else
                w.AddAccount("paper", "My Paper Wallet", new { Wif = wif, ScriptPubKeyType = scriptPubKeyType, w.Network });

            return (PaperAccount)w.Accounts[0];
        }

        Wallet GetWallet()
        {
            if (wallet is null)
            {
                wallet = new Wallet();
                wallet.Init(MNEMONIC);
            }

            return wallet;
        }
    }
}
