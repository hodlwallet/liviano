using System;
using System.Linq;

using Xunit;

using NBitcoin;

using Liviano;
using Liviano.Models;
using Liviano.Exceptions;
using System.Collections.Generic;
using NBitcoin.DataEncoders;

namespace Liviano.Tests.Liviano
{
    public class HdOperationsTest
    {
        [Fact]
        public void GetNetworkTest()
        {
            Assert.Equal(Network.Main, HdOperations.GetNetwork());
            Assert.Equal(Network.Main, HdOperations.GetNetwork("main"));
            Assert.Equal(Network.TestNet, HdOperations.GetNetwork("testnet"));
            Assert.Equal(Network.RegTest, HdOperations.GetNetwork("regtest"));

            Assert.Throws<WalletException>(() => HdOperations.GetNetwork("Liviano"));
        }

        [Fact]
        public void CreateHdPathTest()
        {
            Assert.Equal(
                "m/84'/0'/0'/0/0",
                HdOperations.CreateHdPath(
                    0 /* Bitcoin */,
                    0,
                    false,
                    0
                )
            );

            Assert.Equal(
                "m/84'/0'/0'/1/0",
                HdOperations.CreateHdPath(
                    0 /* Bitcoin */,
                    0,
                    true,
                    0
                )
            );
        }

        [Fact]
        public void GetCointTypeTest()
        {
            Assert.Equal(
                0,
                HdOperations.GetCoinType("m/84'/0'/0'/0/1")
            );

            Assert.Throws<WalletException>(() => {
                HdOperations.GetCoinType("INVALID_HDPATH");
            });
        }

        [Fact]
        public void IsChangeAddressTest()
        {
            Assert.True(HdOperations.IsChangeAddress("m/44'/0'/0'/1/0"));
            Assert.False(HdOperations.IsChangeAddress("m/44'/0'/0'/0/0"));
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
                HdOperations.GetExtendedKey(mnemonic).GetWif(Network.Main).ToString()
            );

            Assert.Equal(
                expectedWifWithPassphrase,
                HdOperations.GetExtendedKey(mnemonic, passphrase).GetWif(Network.Main).ToString()
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
                HdOperations.GetExtendedPublicKey(wif, hdPath, network).GetWif(Network.Main).ToString()
            );
        }

        [Fact]
        public void GetAddressTest()
        {
            string wif = "xpub6Bz4z2m4vevKWG5Mzsr2XYPTZCAjM2DvCpiDH88r7c2gnJRLzxUmJQGocXEyx3cJQ1QApm7aZRZuutoqLMSqS7oVorRajQubMJXvkTi7f4F";

            Assert.Equal(
                "1Cd2SkZ4bUbWk5Etyvq5g2PVJv4gcig1yn",
                HdOperations.GetAddress(wif, 0, false, "main", "p2pkh").ToString()
            );

            Assert.Equal(
                "39k4tnAD4faY5w5GRTKvYCYqQJLQzs94oA",
                HdOperations.GetAddress(wif, 0, false, "main", "p2sh-p2wpkh").ToString()
            );

            Assert.Equal(
                "bc1q0aueqjtnjyyhm7m6unvkkp4c0p3fdt8qjtuq2j",
                HdOperations.GetAddress(wif, 0, false, "main", "p2wpkh").ToString()
            );

            Assert.Equal(
                "bc1q0aueqjtnjyyhm7m6unvkkp4c0p3fdt8qjtuq2j",
                HdOperations.GetAddress(wif, 0, false, "main").ToString()
            );
        }

        [Fact]
        public void GenerateGuessWordsTest()
        {
            string wordToGuess = "float"; // Word in the english dictionary
            string language = "english";
            int amountAround = 4; // Total of 5 becaues the word to guess is added

            string[] guessWords = HdOperations.GenerateGuessWords(wordToGuess, language, amountAround);

            Assert.Contains(wordToGuess, guessWords);
            Assert.Equal(amountAround + 1, guessWords.Length);
        }

        [Fact]
        public void IsInMnemonicAtIndexTest()
        {
            string mnemonic = "ugly dilemma idle crowd toast virus film funny laundry little gossip pair";

            Assert.True(HdOperations.IsInMnemonicAtIndex(mnemonic, "dilemma", 1));
            Assert.False(HdOperations.IsInMnemonicAtIndex(mnemonic, "film", 3));
        }

        [Fact]
        public void Bip84CompatibilityTest()
        {
            Network network = Network.Main;

            // Set Mnemonic and get the priv ext key
            string mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            ExtKey extKey = HdOperations.GetExtendedKey(mnemonic);

            // Creates a new wallet
            Wallet wallet = new Wallet
            {
                Name = "bip84wallet",
                EncryptedSeed = extKey.PrivateKey.GetWif(network).ToString(),
                ChainCode = extKey.ChainCode,
                CreationTime = DateTimeOffset.Now,
                Network = network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot(CoinType.Bitcoin, new List<HdAccount>()){}},
            };

            HdAccount account = wallet.AddNewAccount(CoinType.Bitcoin, DateTimeOffset.Now);

            HdAddress[] newReceivingAddresses = account.CreateAddresses(network, 20).ToArray();
            HdAddress[] newChangeAddressess = account.CreateAddresses(network, 20).ToArray();

            // Verify data with https://iancoleman.io/bip39/ if needed
            Assert.Equal
            (
                "5eb00bbddcf069084889a8ab9155568165f5c453ccb85e70811aaed6f6da5fc19a5ac40b389cd370d086206dec8aa6c43daea6690f20ad3d8d48b2d2ce9e38e4",
                new HexEncoder().EncodeData(new Mnemonic(mnemonic).DeriveSeed())
            );

            Assert.Equal
            (
                CoinType.Bitcoin,
                account.GetCoinType()
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

            Assert.Equal(
                "m/84'/0'/0'",
                account.HdPath
            );

            // Address 0 from Account 0 test
            Assert.Equal
            (
                "bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu",
                newReceivingAddresses[0].Address
            );


            Assert.Equal
            (
                "0330d54fd0dd420a6e5f8d3624f5f3482cae350f79d5f0753bf5beef9c2d91af3c",
                new HexEncoder().EncodeData(newReceivingAddresses[0].PubKey.ScriptPubKey.ToCompressedBytes())
            );

            // Address 1 from account 0 test
            Assert.Equal
            (
                "bc1qnjg0jd8228aq7egyzacy8cys3knf9xvrerkf9g",
                newReceivingAddresses[1].Address
            );


            Assert.Equal
            (
                "03e775fd51f0dfb8cd865d9ff1cca2a158cf651fe997fdc9fee9c1d3b5e995ea77",
                new HexEncoder().EncodeData(newReceivingAddresses[1].PubKey.ScriptPubKey.ToCompressedBytes())
            );

            // Address 2 from account 0 test
            Assert.Equal
            (
                "bc1qp59yckz4ae5c4efgw2s5wfyvrz0ala7rgvuz8z",
                newReceivingAddresses[2].Address
            );


            Assert.Equal
            (
                "038ffea936b2df76bf31220ebd56a34b30c6b86f40d3bd92664e2f5f98488dddfa",
                new HexEncoder().EncodeData(newReceivingAddresses[2].PubKey.ScriptPubKey.ToCompressedBytes())
            );

            // Address 18 from account 0 test
            Assert.Equal
            (
                "bc1qf60uv69k0prrdxkpmh94u9cwmkpkl0t0r02hgh",
                newReceivingAddresses[18].Address
            );


            Assert.Equal
            (
                "02d56ba8cc5cb6c4e3995c2b73e7bc934d2456299cd74cb311d1c8612b46add054",
                new HexEncoder().EncodeData(newReceivingAddresses[18].PubKey.ScriptPubKey.ToCompressedBytes())
            );

            // Address 19 from account 0 test
            Assert.Equal
            (
                "bc1q27yd7vz8m5kz230wuyncfe3pyazez6ah58yzy0",
                newReceivingAddresses[19].Address
            );


            Assert.Equal
            (
                "03fc8771c531b40e1202f91a779faf0a7955cebceb38bd18924163a99dafaaa647",
                new HexEncoder().EncodeData(newReceivingAddresses[19].PubKey.ScriptPubKey.ToCompressedBytes())
            );

            Assert.Equal
            (
                "zpub6rFR7y4Q2AijBEqTUquhVz398htDFrtymD9xYYfG1m4wAcvPhXNfE3EfH1r1ADqtfSdVCToUG868RvUUkgDKf31mGDtKsAYz2oz2AGutZYs",
                ExtPubKey.Parse(account.ExtendedPubKey, network).ToZPub(network)
            );

            Assert.Equal
            (
                "zprvAdG4iTXWBoARxkkzNpNh8r6Qag3irQB8PzEMkAFeTRXxHpbF9z4QgEvBRmfvqWvGp42t42nvgGpNgYSJA9iefm1yYNZKEm7z6qUWCroSQnE",
                ExtKey.Parse(account.ExtendedPrivKey, network).ToZPrv(network)
            );
        }
    }
}