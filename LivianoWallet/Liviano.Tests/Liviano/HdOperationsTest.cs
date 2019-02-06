using System;
using Xunit;
using NBitcoin;
using Liviano.Exceptions;

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
                "m/44'/0'/0'/0/0",
                HdOperations.CreateHdPath(
                    0 /* Bitcoin */,
                    0,
                    false,
                    0
                )
            );

            Assert.Equal(
                "m/44'/0'/0'/1/0",
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
                HdOperations.GetCoinType("m/44'/0'/0'/0/0")
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
	public void IsWordInWordlistTest()
	{
	    string exist = "abstract";
	    string nonExist = "ambiguous";

	    Assert.True(HdOperations.IsWordInWordlist(exist, "english"));
	    Assert.False(HdOperations.IsWordInWordlist(nonExist, "english")); 
	}

	[Fact]
	public void IsValidChecksumTest()
	{
	    string validMnemonic = "ugly dilemma idle crowd toast virus film funny laundry little gossip pair";
	    string invalidMnemonic = "ugly clarify idle crowd toast virus film funny laundry little gossip pair";

	    Assert.True(HdOperations.IsValidChecksum(validMnemonic, "english"));
	    Assert.False(HdOperations.IsValidChecksum(invalidMnemonic, "english"));
	}
    }
}
