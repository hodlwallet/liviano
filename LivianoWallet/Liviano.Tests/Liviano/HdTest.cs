using System;

using Xunit;

using NBitcoin;

using Liviano.Exceptions;
using NBitcoin.DataEncoders;
using Liviano.Bips;
using System.Collections.Generic;

namespace Liviano.Tests.Liviano
{
    public class HdTest
    {
        [Fact]
        public void GetNetworkTest()
        {
            Assert.Equal(Network.Main, Hd.GetNetwork());
            Assert.Equal(Network.Main, Hd.GetNetwork("main"));
            Assert.Equal(Network.TestNet, Hd.GetNetwork("testnet"));
            Assert.Equal(Network.RegTest, Hd.GetNetwork("regtest"));

            Assert.Throws<WalletException>(() => Hd.GetNetwork("Liviano"));
        }

        [Fact]
        public void CreateHdPathTest()
        {
            Assert.Equal(
                "m/84'/0'/0'/0/0",
                Hd.CreateHdPath(
                    0 /* Bitcoin */,
                    0,
                    false,
                    0
                )
            );

            Assert.Equal(
                "m/84'/0'/0'/1/0",
                Hd.CreateHdPath(
                    0 /* Bitcoin */,
                    0,
                    true,
                    0
                )
            );
        }

        [Fact]
        public void IsChangeAddressTest()
        {
            Assert.True(Hd.IsChangeAddress("m/44'/0'/0'/1/0"));
            Assert.False(Hd.IsChangeAddress("m/44'/0'/0'/0/0"));
        }

        [Fact]
        public void GetExtendedKeyTest()
        {
            string mnemonic = "arrow lesson asthma now shadow immense find parrot maid cry bachelor rough total seed what clarify soda oxygen exist sign sphere until quick donor";
            string expectedWif = "xprv9s21ZrQH143K34C4e8hDbVV68AbYEzjeqgv6YKT7wb8mugXxzzoZCRToBy823ffGugbedX3bWV4fW8c3Nz8Yf7FP697yVN9o5JJt2cab3Ny";
            string expectedWifWithPassphrase = "xprv9s21ZrQH143K2qZqvjXNNpuw7JMfTDeSi2gYz1w42jHmqCvHJY1nwzFM5JM39LYGXXGjeYnrBL9KLfp6RMFeSVfBDKVyTjUrn5TVxMt5y4U";
            string passphrase = "liviano password secured by this random number 7";

            Assert.Equal(
                expectedWif,
                Hd.GetExtendedKey(mnemonic).GetWif(Network.Main).ToString()
            );

            Assert.Equal(
                expectedWifWithPassphrase,
                Hd.GetExtendedKey(mnemonic, passphrase).GetWif(Network.Main).ToString()
            );
        }

        [Fact]
        public void GetExtendedPublicKeyTest()
        {
            string wif = "xprv9s21ZrQH143K34C4e8hDbVV68AbYEzjeqgv6YKT7wb8mugXxzzoZCRToBy823ffGugbedX3bWV4fW8c3Nz8Yf7FP697yVN9o5JJt2cab3Ny";
            string hdPath = "m/44'/0'/0'/0/0";
            string network = "main";
            string expectedExtPubKeyWif = "xpub6GvB5UBtkQz8uH4s23mNaU8VMYJn56BaDbow8oqN47WMK7bc4qgLBfGR8uFUcLeYDyBQEafiohbka4HCsoFQaSBKZiuCoKcr8d4nHtCB4P8";

            Assert.Equal(
                expectedExtPubKeyWif,
                Hd.GetExtendedPublicKey(wif, hdPath, network).GetWif(Network.Main).ToString()
            );
        }

        [Fact]
        public void GetAddressTest()
        {
            string wif = "xpub6Bz4z2m4vevKWG5Mzsr2XYPTZCAjM2DvCpiDH88r7c2gnJRLzxUmJQGocXEyx3cJQ1QApm7aZRZuutoqLMSqS7oVorRajQubMJXvkTi7f4F";

            Assert.Equal(
                "1Cd2SkZ4bUbWk5Etyvq5g2PVJv4gcig1yn",
                Hd.GetAddress(wif, 0, false, "main", "p2pkh").ToString()
            );

            Assert.Equal(
                "39k4tnAD4faY5w5GRTKvYCYqQJLQzs94oA",
                Hd.GetAddress(wif, 0, false, "main", "p2sh-p2wpkh").ToString()
            );

            Assert.Equal(
                "bc1q0aueqjtnjyyhm7m6unvkkp4c0p3fdt8qjtuq2j",
                Hd.GetAddress(wif, 0, false, "main", "p2wpkh").ToString()
            );

            Assert.Equal(
                "bc1q0aueqjtnjyyhm7m6unvkkp4c0p3fdt8qjtuq2j",
                Hd.GetAddress(wif, 0, false, "main").ToString()
            );
        }

        [Fact]
        public void GenerateGuessWordsTest()
        {
            string wordToGuess = "float"; // Word in the english dictionary
            string language = "english";
            int amountAround = 4; // Total of 5 becaues the word to guess is added

            string[] guessWords = Hd.GenerateGuessWords(wordToGuess, language, amountAround);

            Assert.Contains(wordToGuess, guessWords);
            Assert.Equal(amountAround + 1, guessWords.Length);
        }

        [Fact]
        public void IsInMnemonicAtIndexTest()
        {
            string mnemonic = "ugly dilemma idle crowd toast virus film funny laundry little gossip pair";

            Assert.True(Hd.IsInMnemonicAtIndex(mnemonic, "dilemma", 1));
            Assert.False(Hd.IsInMnemonicAtIndex(mnemonic, "film", 3));
        }

        [Fact]
        public void Bip84CompatibilityTest()
        {
            Network network = Network.Main;

            // Set Mnemonic and get the priv ext key
            string mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            ExtKey extKey = Hd.GetExtendedKey(mnemonic);

            // Creates a new wallet
            Wallet wallet = new Wallet();
            wallet.Init(mnemonic);

            wallet.AddAccount("bip84", options: new object {});
            var account = wallet.Accounts[0];

            var publicKeys = new List<string> {};
            var extPubKey = account.ExtPubKey;

            Assert.Equal
            (
                "zpub6rFR7y4Q2AijBEqTUquhVz398htDFrtymD9xYYfG1m4wAcvPhXNfE3EfH1r1ADqtfSdVCToUG868RvUUkgDKf31mGDtKsAYz2oz2AGutZYs",
                extPubKey.ToZPub()
            );

            // Get all public keys
            for (int i = 0; i < 20; i++)
            {
                var pubKey = Hd.GeneratePublicKey(account.Network, extPubKey.ToString(), i, false);

                publicKeys.Add(new HexEncoder().EncodeData(pubKey.ToBytes()));
            }

            // Get all addresses
            BitcoinAddress[] newReceivingAddresses = account.GetReceiveAddress(20);

            // Verify data with https://iancoleman.io/bip39/ if needed
            Assert.Equal
            (
                "5eb00bbddcf069084889a8ab9155568165f5c453ccb85e70811aaed6f6da5fc19a5ac40b389cd370d086206dec8aa6c43daea6690f20ad3d8d48b2d2ce9e38e4",
                new HexEncoder().EncodeData(new Mnemonic(mnemonic).DeriveSeed())
            );

            Assert.Equal
            (
                "zprvAWgYBBk7JR8Gjrh4UJQ2uJdG1r3WNRRfURiABBE3RvMXYSrRJL62XuezvGdPvG6GFBZduosCc1YP5wixPox7zhZLfiUm8aunE96BBa4Kei5",
                extKey.ToZPrv(network)
            );

            Assert.Matches
            (
                @"m/84'/",
                account.HdPath
            );

            Assert.Equal
            (
                "m/84'/0'/0'",
                account.HdPath
            );

            // Address 0 from Account 0 test
            Assert.Equal
            (
                "bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu",
                newReceivingAddresses[0].ToString()
            );


            Assert.Equal
            (
                "0330d54fd0dd420a6e5f8d3624f5f3482cae350f79d5f0753bf5beef9c2d91af3c",
                publicKeys[0]
            );

            // Address 1 from account 0 test
            Assert.Equal
            (
                "bc1qnjg0jd8228aq7egyzacy8cys3knf9xvrerkf9g",
                newReceivingAddresses[1].ToString()
            );


            Assert.Equal
            (
                "03e775fd51f0dfb8cd865d9ff1cca2a158cf651fe997fdc9fee9c1d3b5e995ea77",
                publicKeys[1]
            );

            // Address 2 from account 0 test
            Assert.Equal
            (
                "bc1qp59yckz4ae5c4efgw2s5wfyvrz0ala7rgvuz8z",
                newReceivingAddresses[2].ToString()
            );


            Assert.Equal
            (
                "038ffea936b2df76bf31220ebd56a34b30c6b86f40d3bd92664e2f5f98488dddfa",
                publicKeys[2]
            );

            // Address 18 from account 0 test
            Assert.Equal
            (
                "bc1qf60uv69k0prrdxkpmh94u9cwmkpkl0t0r02hgh",
                newReceivingAddresses[18].ToString()
            );


            Assert.Equal
            (
                "03de7490bcca92a2fb57d782c3fd60548ce3a842cad6f3a8d4e76d1f2ff7fcdb89",
                publicKeys[3]
            );

            // Address 19 from account 0 test
            Assert.Equal
            (
                "bc1q27yd7vz8m5kz230wuyncfe3pyazez6ah58yzy0",
                newReceivingAddresses[19].ToString()
            );


            Assert.Equal
            (
                "03995137c8eb3b223c904259e9b571a8939a0ec99b0717684c3936407ca8538c1b",
                publicKeys[4]
            );

            Assert.Equal
            (
                "zpub6rFR7y4Q2AijBEqTUquhVz398htDFrtymD9xYYfG1m4wAcvPhXNfE3EfH1r1ADqtfSdVCToUG868RvUUkgDKf31mGDtKsAYz2oz2AGutZYs",
                account.ExtPubKey.ExtPubKey.ToZPub(network)
            );

            Assert.Equal
            (
                "zprvAdG4iTXWBoARxkkzNpNh8r6Qag3irQB8PzEMkAFeTRXxHpbF9z4QgEvBRmfvqWvGp42t42nvgGpNgYSJA9iefm1yYNZKEm7z6qUWCroSQnE",
                account.ExtKey.ExtKey.ToZPrv(network)
            );
        }


        [Fact]
        public void Bip84CompatibilityWithPasswordTest()
        {
            Network network = Network.Main;

            // Set Mnemonic and get the priv ext key
            string mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            string password = "test";
            ExtKey extKey = Hd.GetExtendedKey(mnemonic, password);

            // Creates a new wallet
            Wallet wallet = new Wallet();
            wallet.Init(mnemonic, passphrase: password, network: network);

            wallet.AddAccount("bip84");
            var account = wallet.Accounts[0];

            var publicKeys = new List<string> {};
            var extPubKey = account.ExtPubKey;

            BitcoinAddress[] newReceivingAddresses = account.GetReceiveAddress(20);

            Assert.Equal
            (
                "zpub6s6e8jg36PqLxbsZuWBudDTimzJDPH8hbRsxz8YH5YtxwivCQcH33z8TFhMSy2UHZLeCQkm6nwr5SN7T3E3NtRQu54WZYvQ6i89eWB3BL5Y",
                extPubKey.ToZPub()
            );

            // Get all public keys
            for (int i = 0; i < 20; i++)
            {
                var pubKey = Hd.GeneratePublicKey(account.Network, extPubKey.ToString(), i, false);

                publicKeys.Add(new HexEncoder().EncodeData(pubKey.ToBytes()));
            }

            // Verify data with https://iancoleman.io/bip39/ if needed
            Assert.Equal
            (
                "0bcdfe83aa48a793f8c68f09eee46bce8aa1fe5d4eb76381dff21d1690259e58a9d609e7c6b487d7a4b78230c22465bf727e7f864f386262fc0dea12ec040e7c",
                new HexEncoder().EncodeData(new Mnemonic(mnemonic).DeriveSeed(password))
            );

            Assert.Equal
            (
                "zprvAWgYBBk7JR8GkojhAZB9mNYaZaYoourMf3qn8q6Q3HMRPX2pRVUjEg1KqojcscbyGJUSKbnvpGqsHRDZ1mqq6RpHZ6LnQxmmnExDh293HYZ",
                extKey.ToZPrv(network)
            );

            Assert.Matches
            (
                @"m/84'/",
                account.HdPath
            );

            Assert.Equal
            (
                "m/84'/0'/0'",
                account.HdPath
            );

            // Address 0 from Account 0 test
            Assert.Equal
            (
                "bc1q0h0u48k0hx0m9uhrpzpjsh4h9v2z5jhkvs9w94",
                newReceivingAddresses[0].ToString()
            );


            Assert.Equal
            (
                "03d21ca564c77d4d750e29ac6c4b5d951790e1b49cbbdaed862a2525e480cb956f",
                publicKeys[0]
            );

            // Address 1 from account 0 test
            Assert.Equal
            (
                "bc1qrhcfphe7tjdtwrke999n5rtyv6uzn5m70xghsa",
                newReceivingAddresses[1].ToString()
            );


            Assert.Equal
            (
                "03c31175ae2639e2158122f4793625696310b0c5ee5e9b7710b13b8a8f70c7793c",
                publicKeys[1]
            );

            // Address 2 from account 0 test
            Assert.Equal
            (
                "bc1qgnh247772ta040qknw6pje2ues5udpljwxwlu5",
                newReceivingAddresses[2].ToString()
            );


            Assert.Equal
            (
                "02111472e6f5fdf351023328f2257549a4a69ec4f69b09d5a26580b90cb98fa904",
                publicKeys[2]
            );

            // Address 18 from account 0 test
            Assert.Equal
            (
                "bc1q07y8zq6ach2tz8xvjnfwye5syyh4e6vc25jwz6",
                newReceivingAddresses[18].ToString()
            );


            Assert.Equal
            (
                "0280b51871150e6bc26d33fde82529f6f8eeee0e982e7b2852ded86ecb62dc4f73",
                publicKeys[3]
            );

            // Address 19 from account 0 test
            Assert.Equal
            (
                "bc1qx2vpc2eu4065lv4qrjj4wgdd4wee3lv2ujj5eq",
                newReceivingAddresses[19].ToString()
            );


            Assert.Equal
            (
                "0373a2019496b63d56ce4c975dac016df65694b706ed477b810329a9e16d22167c",
                publicKeys[4]
            );

            Assert.Equal
            (
                "zpub6s6e8jg36PqLxbsZuWBudDTimzJDPH8hbRsxz8YH5YtxwivCQcH33z8TFhMSy2UHZLeCQkm6nwr5SN7T3E3NtRQu54WZYvQ6i89eWB3BL5Y",
                account.ExtPubKey.ExtPubKey.ToZPub(network)
            );

            Assert.Equal
            (
                "zprvAe7HjE99G2H3k7o6oUeuG5WzDxTiypQrECxNBk8fXDMz4vb3s4xnWBoyQPpgDAsPFPjtVZGU4PppBzBTw3AiyoYsy23MKeqvLTP2SPypLA9",
                account.ExtKey.ExtKey.ToZPrv(network)
            );
        }
    }
}
