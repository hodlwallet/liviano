using Xunit;

using NBitcoin;

using Liviano.Bips;

namespace Liviano.Tests.Liviano
{
    public class Bip84Test
    {
        /// <summary>
        /// Test vectors for BIP 84
        /// From: https://github.com/bitcoin/bips/blob/master/bip-0084.mediawiki#test-vectors
        /// </summary>
        [Fact]
        public void BIP84Test()
        {
            Network network = Network.Main;

            string mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            string rootpriv = "zprvAWgYBBk7JR8Gjrh4UJQ2uJdG1r3WNRRfURiABBE3RvMXYSrRJL62XuezvGdPvG6GFBZduosCc1YP5wixPox7zhZLfiUm8aunE96BBa4Kei5";
            string rootpub = "zpub6jftahH18ngZxLmXaKw3GSZzZsszmt9WqedkyZdezFtWRFBZqsQH5hyUmb4pCEeZGmVfQuP5bedXTB8is6fTv19U1GQRyQUKQGUTzyHACMF";

            ExtKey extKey = Hd.GetExtendedKey(mnemonic);
            ExtPubKey extPubKey = extKey.Neuter();
            string zprv = Bip84.GetZPrv(extKey, network);
            string zpub = Bip84.GetZPub(extPubKey, network);

            Assert.Equal(
                rootpriv,
                zprv
            );

            Assert.Equal(
                rootpub,
                zpub
            );

            // Account 0, root = m/84'/0'/0'
            string xpriv = "zprvAdG4iTXWBoARxkkzNpNh8r6Qag3irQB8PzEMkAFeTRXxHpbF9z4QgEvBRmfvqWvGp42t42nvgGpNgYSJA9iefm1yYNZKEm7z6qUWCroSQnE";
            string xpub = "zpub6rFR7y4Q2AijBEqTUquhVz398htDFrtymD9xYYfG1m4wAcvPhXNfE3EfH1r1ADqtfSdVCToUG868RvUUkgDKf31mGDtKsAYz2oz2AGutZYs";

            ExtKey accountRootExtKey = extKey.Derive(new KeyPath("m/84'/0'/0'"));
            ExtPubKey accountRootExtPubKey = accountRootExtKey.Neuter();

            Assert.Equal(
                xpriv,
                accountRootExtKey.ToZPrv(network)
            );

            Assert.Equal(
                xpub,
                accountRootExtPubKey.ToZPub(network)
            );

            // Account 0, first receiving address = m/84'/0'/0'/0/0
            string privkey = "KyZpNDKnfs94vbrwhJneDi77V6jF64PWPF8x5cdJb8ifgg2DUc9d";
            string pubkey = "0330d54fd0dd420a6e5f8d3624f5f3482cae350f79d5f0753bf5beef9c2d91af3c";
            string address = "bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu";

            ExtKey account0ExtKey = extKey.Derive(new KeyPath("m/84'/0'/0'/0/0"));
            ExtPubKey account0ExtPubKey = account0ExtKey.Neuter();

            Assert.Equal(
                privkey,
                account0ExtKey.PrivateKey.GetWif(network).ToString()
            );

            Assert.Equal(
                pubkey,
                account0ExtPubKey.PubKey.ToHex()
            );

            Assert.Equal(
                address,
                account0ExtPubKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, network).ToString()
            );

            // Account 0, second receiving address = m/84'/0'/0'/0/1
            privkey = "Kxpf5b8p3qX56DKEe5NqWbNUP9MnqoRFzZwHRtsFqhzuvUJsYZCy";
            pubkey = "03e775fd51f0dfb8cd865d9ff1cca2a158cf651fe997fdc9fee9c1d3b5e995ea77";
            address = "bc1qnjg0jd8228aq7egyzacy8cys3knf9xvrerkf9g";

            account0ExtKey = extKey.Derive(new KeyPath("m/84'/0'/0'/0/1"));
            account0ExtPubKey = account0ExtKey.Neuter();

            Assert.Equal(
                privkey,
                account0ExtKey.PrivateKey.GetWif(network).ToString()
            );

            Assert.Equal(
                pubkey,
                account0ExtPubKey.PubKey.ToHex()
            );

            Assert.Equal(
                address,
                account0ExtPubKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, network).ToString()
            );

            // Account 0, first change address = m/84'/0'/0'/1/0
            privkey = "KxuoxufJL5csa1Wieb2kp29VNdn92Us8CoaUG3aGtPtcF3AzeXvF";
            pubkey = "03025324888e429ab8e3dbaf1f7802648b9cd01e9b418485c5fa4c1b9b5700e1a6";
            address = "bc1q8c6fshw2dlwun7ekn9qwf37cu2rn755upcp6el";

            account0ExtKey = extKey.Derive(new KeyPath("m/84'/0'/0'/1/0"));
            account0ExtPubKey = account0ExtKey.Neuter();

            Assert.Equal(
                privkey,
                account0ExtKey.PrivateKey.GetWif(network).ToString()
            );

            Assert.Equal(
                pubkey,
                account0ExtPubKey.PubKey.ToHex()
            );

            Assert.Equal(
                address,
                account0ExtPubKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, network).ToString()
            );
        }

        [Fact]
        public void Bip84ParseZPubAndZPrvTest()
        {
            string xprv = "xprv9s21ZrQH143K49v6XmzHEobWSf8es1MUKtmtBDhZc3tseCQc9AxDMFi2JH6Hj5dCG85FVdA8CLe9QNxzvkiudQM33wVMP6Tyq8YVjF83e1X";
            string zprv = "zprvAWgYBBk7JR8GkkJLCVZXeynWnbRYkFLUA7pKk1VLN4edkQ34eVHLbP2JLh1Titw35QJrzaMF7fMFAxC8N9YwDsiEnctCYv6xNafnWTswjhM";

            string xpub = "xpub6DGSQxp7zdbVm7aj3Sj934kpDVLAhyti2AMhPZXDCNYpCegyRf1diFY2qSJyJXsQEmtrU46MHdzG7JVKqueUCUTCjVR9F6jfkj6u3BXQX1G";
            string zpub = "zpub6rvy2J9xHzgTThxxiAJPTEwpZRd4bDshrPQ8xMJyxPJaJrKRvyLkxNrJsrE9JMBF448Ty1HUCxhMssiTHJUVnwpQUAozQvNeJBEBpLVdxuy";

            ExtPubKey extPubKey;
            ExtKey extKey;

            extPubKey = Bip84.ParseZPub(zpub);

            Assert.Equal(
                xpub,
                extPubKey.GetWif(Network.Main).ToString()
            );

            extKey = Bip84.ParseZPrv(zprv);

            Assert.Equal(
                xprv,
                extKey.GetWif(Network.Main).ToString()
            );
        }

        [Fact]
        public void WasabiWalletAddressTest()
        {
            var extKey = ExtKey.Parse("xprv9s21ZrQH143K49v6XmzHEobWSf8es1MUKtmtBDhZc3tseCQc9AxDMFi2JH6Hj5dCG85FVdA8CLe9QNxzvkiudQM33wVMP6Tyq8YVjF83e1X", Network.Main);
            var extPubKey = extKey.Derive(new KeyPath("84'/0'/0'/0/1")).Neuter();
            var expectedAddress = "bc1q48mu6gda7z73dc0u4lfjxrx4chpsxqs3e3ccld";

            Assert.Equal(
                expectedAddress,
                extPubKey.PubKey.WitHash.GetAddress(Network.Main).ToString()
            );
        }
    }
}
