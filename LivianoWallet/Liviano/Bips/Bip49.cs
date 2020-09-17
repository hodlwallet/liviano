//
// Bip49.cs
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
    /// Implements BIP49's utility functions to parse and convert BIP44's format prefixes (xpub and xprv) to BIP49's prefixes (ypub and yprv)
    /// read details here: https://github.com/bitcoin/bips/blob/master/bip-0049.mediawiki
    /// </summary>
    public static class Bip49
    {
        public static ExtKey ParseYPrv(string yprv)
        {
            var network = GetNetwork(yprv);
            var encoder = network.GetBase58CheckEncoder();

            byte[] yPrvData = encoder.DecodeData(yprv);
            byte[] newPrefix = Utils.ToBytes(network == Network.Main ? 0x0488ade4U : 0x04358394U, false);

            for (int i = 0; i < 4; i++)
                yPrvData[i] = newPrefix[i];

            return new BitcoinExtKey(encoder.EncodeData(yPrvData), network);
        }

        public static ExtPubKey ParseYPub(string ypub)
        {
            var network = GetNetwork(ypub);
            var encoder = network.GetBase58CheckEncoder();

            byte[] yPubData = encoder.DecodeData(ypub);
            byte[] newPrefix = Utils.ToBytes(network == Network.Main ? 0x0488b21eU : 0x043587cfU, false);

            for (int i = 0; i < 4; i++)
                yPubData[i] = newPrefix[i];

            return new BitcoinExtPubKey(encoder.EncodeData(yPubData), network);
        }

        public static string ToYPrv(this ExtKey extKey, Network network)
        {
            byte[] data = extKey.ToBytes();
            byte[] version = Utils.ToBytes(network == Network.Main ? 0x049d7878U : 0x044a4e28U, false);

            return Encoders.Base58Check.EncodeData(version.Concat(data).ToArray());
        }

        public static string ToYPub(this ExtPubKey extPubKey, Network network)
        {
            byte[] data = extPubKey.ToBytes();
            byte[] version = Utils.ToBytes(network == Network.Main ? 0x049d7cb2U : 0x044a5262U, false);

            return Encoders.Base58Check.EncodeData(version.Concat(data).ToArray());
        }


        public static string GetYPrv(ExtKey extKey, Network network)
        {
            return extKey.ToYPrv(network);
        }

        public static string GetYPub(ExtPubKey extPubKey, Network network)
        {
            return extPubKey.ToYPub(network);
        }

        static Network GetNetwork(string str)
        {
            return str.StartsWith("y") ? Network.Main : Network.TestNet;
        }
    }
}

