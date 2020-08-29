//
// Wallet.cs
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
using System.Linq;

using NBitcoin;
using NBitcoin.DataEncoders;

namespace Liviano.Bips
{
    /// <summary>
    /// Implements BIP84's utility functions to parse and convert BIP44's format prefixes (xpub and xprv) to BIP84's prefixes (zpub and zprv)
    /// read details here: https://github.com/bitcoin/bips/blob/master/bip-0084.mediawiki
    /// </summary>
    public static class Bip84
    {
        public static ExtKey ParseZPrv(string zprv)
        {
            var network = GetNetwork(zprv);
            var encoder = network.GetBase58CheckEncoder();

            byte[] zPrvData = encoder.DecodeData(zprv);
            byte[] newPrefix = Utils.ToBytes(network == Network.Main ? 0x0488ade4U : 0x04358394U, false);

            for (int i = 0; i < 4; i++)
                zPrvData[i] = newPrefix[i];

            return new BitcoinExtKey(encoder.EncodeData(zPrvData), network);
        }

        public static ExtPubKey ParseZPub(string zpub)
        {
            var network = GetNetwork(zpub);
            var encoder = network.GetBase58CheckEncoder();

            byte[] zPubData = encoder.DecodeData(zpub);
            byte[] newPrefix = Utils.ToBytes(network == Network.Main ? 0x0488b21eU : 0x043587cfU, false);

            for (int i = 0; i < 4; i++)
                zPubData[i] = newPrefix[i];

            return new BitcoinExtPubKey(encoder.EncodeData(zPubData), network);
        }

        public static string ToZPub(this ExtPubKey extPubKey, Network network)
        {
            byte[] data = extPubKey.ToBytes();
            byte[] version = Utils.ToBytes(network == Network.Main ? 0x04B24746U : 0x045F1CF6U, false);

            return Encoders.Base58Check.EncodeData(version.Concat(data).ToArray());
        }

        public static string ToZPrv(this ExtKey extKey, Network network)
        {
            byte[] data = extKey.ToBytes();
            byte[] version = Utils.ToBytes(network == Network.Main ? 0x04B2430CU : 0x045F18BCU, false);

            return Encoders.Base58Check.EncodeData(version.Concat(data).ToArray());
        }

        public static string GetZPrv(ExtKey extKey, Network network)
        {
            return extKey.ToZPrv(network);
        }

        public static string GetZPub(ExtPubKey extPubKey, Network network)
        {
            return extPubKey.ToZPub(network);
        }

        private static Network GetNetwork(string str)
        {
            return str.StartsWith("z") ? Network.Main : Network.TestNet;
        }
    }
}
