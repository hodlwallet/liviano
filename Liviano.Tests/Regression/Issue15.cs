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
        [Fact]
        public void Tx_28269d0841e3c0824d04659780e1cb1dbe8e806619eec1bfcba80e3fb1c726c2()
        {
            RunTest(
                hex: "010000000001019a204a7c27c2f6ff8c41e959f8aab27f9d0f8dde89a54ea607e910ed415ee0960000000000ffffffff01d09b000000000000160014269a7224acf8a31f7be59efe7db8d9ba3a204ff602483045022100a838a115f83ab0ed3c98d3c351944c6805600cd0d2e1d8c7f0807383a88dfa5f02203c220db8df48637b359d1043a334521a299c508d383ca972569e70fb11eba1420121039191d34ca380fac499a072e5762217bf6fe6b7ea157faaf771c78c89ef444c4300000000",
                height: 2138098,
                headerHex: "04e0ff2f8866e4a740d838f84865a91ddcc061519fe10c7c842b4eed0500000000000000f53f48e06efc021d6b07dbc20db998816b9774d5ba3f4d4a67458fe3d0ef0e2fd3baf561cbcd001a10ffee58"
            );
        }

        [Fact]
        public void Tx_5fa3cffef5beec913e700b1315b1940d079410898cf9ce672dbb65b138fd275b()
        {
            RunTest(
                hex: "010000000001013a9440ecf7dc4709394c296129334744ec05f4534ba55a0b3cab28f5edc9a5b40000000000ffffffff01c074000000000000160014269a7224acf8a31f7be59efe7db8d9ba3a204ff60247304402202f796dee11b6b86c79e8b85ba541706039d7f208f52d31b7c9d35f56081e662802207021f009d6d95382d3d3b3108d43c1f823cf0579b48e9c49d0dec6dc556c6c23012103610008903596be72b0be7ed0dc9c2165fa6c82677ad77c706641a771a9bea2fd00000000",
                height: 1933360,
                headerHex: "00000020701b602f78e36d534a40fcb957d876c7f677b9f847d0420f300000000000000026e6446261a8fab13a8c2986171a1b490d224d564815553ee5f501c9b2b50994ec971b60c0f13419010d591d"
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