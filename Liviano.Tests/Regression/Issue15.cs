//
// Issue15.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2022 HODL Wallet
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
using System.Diagnostics;
using System.IO;
using System.Reflection;

using NBitcoin;
using Xunit;

using Liviano.Exceptions;
using Liviano.Interfaces;
using Liviano.Models;
using Liviano.Storages;

namespace Liviano.Tests.Regression
{
    /// <summary>
    /// Regression #15 https://github.com/hodlwallet/liviano/issues/15
    /// 
    /// Network = TestNet
    /// Mnemonic = abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about
    /// HdPath = m / 84'/1'/0'
    /// Total Txs = 133
    /// Adress types = Segwit
    /// 
    /// Errors on transactions of the abandon...about mnemonic run --doctor to fin the txs
    /// 
    /// All data coming from http://tapi.qbit.ninja/tx/{txId}
    /// </summary>
    public class Issue15
    {
        // This is the wallet on file for the tests
        const string WALLET_ID = "62d50d77-a430-4177-a3ec-d548db3261a7";
        readonly IWallet wallet;

        public Issue15()
        {
            wallet = Load(WALLET_ID, Network.TestNet);
        }

        [Fact]
        public void Tx_9fca06b2a6469d401f33ef86d29ea4cdd832c35e6384f5df663e9fd4a95df3ac()
        {
            // A tx that was working already
            RunTest(
                hex: "01000000000101b5561450458314097d23e48ce2ef6eefe2581ed226a03ee27f843929d8a090440000000000fdffffff0240420f0000000000160014d0c4a3ef09e997b6e99e397e518fe3e41a118ca1d8bb6f01000000001600141eb17d4dcc40dcc15091597e5367438ef3e6eec1024730440220391911c8bc0e59ac10d339a0e299f9477857a9e86fc2beb9c68a40ed6bc325c202203571b8e169f53e8b949786164b5fb80dc3e42ddad78d1b9f0777695a3ea23efc0121024ea4015948ecb68ff3ff4b5092ecc77188f2f63766a5c45db7f1822c88cb1b1606391300",
                height: 1259783,
                headerHex: "00000020b2af82b123da13087ef087ee541413c87513e48c589739784049f960000000000ab56ceb5c2884c461d3c349cc212ee23c357939f4d7c43e65f8105a8331d8e8fd6c645affff001d2cbead41"
            );
        }

        [Fact]
        public void Tx_3e610a6f94ad1b0f0799e196678cd580696628fc3aef7b73f26818cda6bb5ac4()
        {
            RunTest(
                hex: "020000000001014f56343b252f32a922481a7f40c24e29c9f305b1aa9230f32c8752f9e7961cec0100000000fdffffff019395680000000000160014e0dd6c1d3b99a5fc7a9da4b648ce4a7ac7f01031024730440220752e94aaccc3aaafe0eae36f2f7e62148f589adc38f8d320e780ca96d19baa3502202b35f883b1baec68acd6e0ed6e063f25865c99ecd0991ac469e153b5ab5b9f78012103b858ef5d6e074c4738b37feabb74db46d80403e3e560694890a9a5e0bc22693c05022100",
                height: 2163206,
                headerHex: "00000020b8cb584d5883ee386fa27549a3518e8ae64980145ba257159ab9c876000000001649e2c98d8460ecce2d983c8354b598747234314450c3f41b4ea6734ab7b6429fd01362ffff001d046a500c"
            );
        }

        void RunTest(string hex, int height, string headerHex)
        {
            var network = Network.TestNet;
            var account = wallet.CurrentAccount;
            var header = BlockHeader.Parse(headerHex, network);
            var tx = Tx.CreateFromHex(hex, height, header, account, network);

            Assert.NotNull(tx);

            if (tx.IsReceive)
            {
                Assert.Null(tx.SentScriptPubKey);
                Assert.NotNull(tx.ScriptPubKey);
            }
            else if (tx.IsSend)
            {
                Assert.Null(tx.ScriptPubKey);
                Assert.NotNull(tx.SentScriptPubKey);
            }
            else
            {
                Assert.False(!tx.IsReceive && !tx.IsSend);
            }

            // TODO add missing conditions from --doctor
        }

        static IWallet Load(string walletId, Network network, string passphrase = null, bool skipAuth = false)
        {
            var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var directory = Path.Combine(baseDirectory, "Regression", "Files", "Wallets");

            var storage = new FileSystemWalletStorage(walletId, network, directory);

            if (!storage.Exists())
            {
                Debug.WriteLine($"[Load] Wallet {walletId} doesn't exists. Make sure you're on the right network");

                throw new WalletException("Invalid wallet id");
            }

            return storage.Load(passphrase, out WalletException _, skipAuth);
        }
    }
}