using System;
using System.Linq;
using System.Collections.Generic;

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
            Network network = GetNetwork(zprv);
            Base58CheckEncoder encoder = network.GetBase58CheckEncoder();

            byte[] zPrvData = encoder.DecodeData(zprv);
            byte[] newPrefix = Utils.ToBytes(network == Network.Main ? 0x0488ade4U : 0x04358394U, false);

            for (int i = 0; i < 4; i++)
                zPrvData[i] = newPrefix[i];

            return new BitcoinExtKey(encoder.EncodeData(zPrvData), network);
        }

        public static ExtPubKey ParseZPub(string zpub)
        {
            Network network = GetNetwork(zpub);
            Base58CheckEncoder encoder = network.GetBase58CheckEncoder();

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