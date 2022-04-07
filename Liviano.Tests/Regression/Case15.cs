//
// Case15.cs
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
using NBitcoin;
using Xunit;

using Liviano.Models;

namespace Liviano.Tests.Regression
{
    /// <summary>
    /// Issues #15 https://github.com/hodlwallet/liviano/issues/15
    /// 
    /// Network = TestNet
    /// Mnemonic = abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about
    /// HdPath = m / 84'/1'/0'
    /// Total Txs = 133
    /// Adress types = Segwit
    /// 
    /// Errors on transactions of the abandon...about mnemonic run --doctor to fin the txs
    /// </summary>
    public class Case15
    {
        const string MNEMONIC = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        [Fact]
        public void Tx_3e610a6f94ad1b0f0799e196678cd580696628fc3aef7b73f26818cda6bb5ac4()
        {
            var txId = "3e610a6f94ad1b0f0799e196678cd580696628fc3aef7b73f26818cda6bb5ac4";
            var hex = "020000000001014f56343b252f32a922481a7f40c24e29c9f305b1aa9230f32c8752f9e7961cec0100000000fdffffff019395680000000000160014e0dd6c1d3b99a5fc7a9da4b648ce4a7ac7f01031024730440220752e94aaccc3aaafe0eae36f2f7e62148f589adc38f8d320e780ca96d19baa3502202b35f883b1baec68acd6e0ed6e063f25865c99ecd0991ac469e153b5ab5b9f78012103b858ef5d6e074c4738b37feabb74db46d80403e3e560694890a9a5e0bc22693c05022100";
            var height = 2163206;
            var headerHex = "00000020b8cb584d5883ee386fa27549a3518e8ae64980145ba257159ab9c876000000001649e2c98d8460ecce2d983c8354b598747234314450c3f41b4ea6734ab7b6429fd01362ffff001d046a500c";
            var network = Network.TestNet;

            var wallet = new Wallet()
            {
                Name = "abandon...about Wallet"
            };

            wallet.Init(MNEMONIC);
            wallet.AddAccount("bip84", "Test Account");

            var account = wallet.CurrentAccount;
            var header = BlockHeader.Parse(headerHex, network);

            var tx = Tx.CreateFromHex(hex, height, header, account, network);

            Assert.NotNull(tx);
        }
    }
}
