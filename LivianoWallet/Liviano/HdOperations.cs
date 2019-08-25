using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using NBitcoin;

using Liviano.Models;
using Liviano.Utilities;
using Liviano.Exceptions;
using Liviano.Managers;
using NBitcoin.DataEncoders;

namespace Liviano
{
    public static class HdOperations
    {
        /// <summary>
        /// a list of accepted mnemonic wordlists
        /// </summary>
        /// <value>an array of strings</value>
        public static readonly string[] AcceptedMnemonicWordlists = new string[] { "chinese_simplified", "chinese_traditional", "english", "french", "japanese", "portuguese_brazil", "spanish" };

        public static readonly int[] AcceptedMnemonicWordCount = new int[] { 12, 15, 18, 21, 24 };

        public static readonly string[] AcceptedNetworks = new string[] { "main", "testnet", "regtest" };

        /// <summary>
        /// Generates an HD public key derived from an extended public key.
        /// </summary>
        /// <param name="accountExtPubKey">The extended public key used to generate child keys.</param>
        /// <param name="index">The index of the child key to generate.</param>
        /// <param name="isChange">A value indicating whether the public key to generate corresponds to a change address.</param>
        /// <returns>
        /// An HD public key derived from an extended public key.
        /// </returns>
        public static PubKey GeneratePublicKey(string accountExtPubKey, int index, bool isChange)
        {
            Guard.NotEmpty(accountExtPubKey, nameof(accountExtPubKey));

            int change = isChange ? 1 : 0;
            var keyPath = new KeyPath($"{change}/{index}");
            ExtPubKey extPubKey = ExtPubKey.Parse(accountExtPubKey).Derive(keyPath);
            return extPubKey.PubKey;
        }

        public static BitcoinAddress GetAddress(string extPubKeyWif, int index, bool isChange, string network, string addressType = null)
        {
            PubKey pubKey = GeneratePublicKey(extPubKeyWif, index, isChange);
            
            switch (addressType)
            {
                case "p2pkh":
                    return pubKey.Hash.GetAddress(GetNetwork(network));
                case "p2sh-p2wpkh":
                    return pubKey.WitHash.ScriptPubKey.Hash.GetAddress(GetNetwork(network));
                case "p2wpkh":
                    return pubKey.WitHash.GetAddress(GetNetwork(network));
                default: // Default to bech32 (p2wpkh)
                    return pubKey.WitHash.GetAddress(GetNetwork(network));
            }
        }

        public static Script GetScriptPubKey(string address, string network)
        {
            return BitcoinAddress.Create(address, GetNetwork(network)).ScriptPubKey;
        }

        public static Network GetNetwork(string network = "main")
        {
            Guard.NotNull(network, nameof(network));

            switch (network.ToLower())
            {
                case "main":
                    return Network.Main;
                case "testnet":
                    return Network.TestNet;
                case "regtest":
                    return Network.RegTest;
                default:
                    throw new WalletException($"Invalid network {network}, valid networks are {string.Join(", ", AcceptedNetworks)}");
            }
        }

        /// <summary>
        /// Gets the extended private key for an account.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended private key.</param>
        /// <param name="chainCode">The chain code used in creating the extended private key.</param>
        /// <param name="hdPath">The HD path of the account for which to get the extended private key.</param>
        /// <param name="network">The network for which to generate this extended private key.</param>
        /// <returns></returns>
        public static ExtKey GetExtendedPrivateKey(Key privateKey, byte[] chainCode, string hdPath, Network network)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));
            Guard.NotEmpty(hdPath, nameof(hdPath));
            Guard.NotNull(network, nameof(network));

            // Get the extended key.
            return new ExtKey(privateKey, chainCode).Derive(new KeyPath(hdPath));
        }

        /// <summary>
        /// Gets the extended public key for an account.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended public key.</param>
        /// <param name="chainCode">The chain code used in creating the extended public key.</param>
        /// <param name="coinType">Type of the coin of the account for which to generate an extended public key.</param>
        /// <param name="accountIndex">Index of the account for which to generate an extended public key.</param>
        /// <returns>The extended public key for an account, used to derive child keys.</returns>
        public static ExtPubKey GetExtendedPublicKey(Key privateKey, byte[] chainCode, int coinType, int accountIndex)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));

            string accountHdPath = GetAccountHdPath(coinType, accountIndex);
            return GetExtendedPublicKey(privateKey, chainCode, accountHdPath);
        }

        /// <summary>
        /// Gets the extended public key corresponding to an HD path.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended public key.</param>
        /// <param name="chainCode">The chain code used in creating the extended public key.</param>
        /// <param name="hdPath">The HD path for which to get the extended public key.</param>
        /// <returns>The extended public key, used to derive child keys.</returns>
        public static ExtPubKey GetExtendedPublicKey(Key privateKey, byte[] chainCode, string hdPath)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));
            Guard.NotEmpty(hdPath, nameof(hdPath));

            // get extended private key
            var seedExtKey = new ExtKey(privateKey, chainCode);
            ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(hdPath));
            ExtPubKey extPubKey = addressExtKey.Neuter();
            return extPubKey;
        }

        public static ExtPubKey GetExtendedPublicKey(ExtKey extKey, string hdPath)
        {
            Guard.NotNull(extKey, nameof(extKey));
            Guard.NotEmpty(hdPath, nameof(hdPath));

            ExtKey addressExtKey = extKey.Derive(new KeyPath(hdPath));
            ExtPubKey extPubKey = addressExtKey.Neuter();

            return extPubKey;
        }

        public static ExtPubKey GetExtendedPublicKey(string wif, string hdPath, string network)
        {
            Guard.NotEmpty(wif, nameof(wif));
            Guard.NotEmpty(hdPath, nameof(hdPath));

            ExtKey extKey = ExtKey.Parse(wif, GetNetwork(network));

            return GetExtendedPublicKey(extKey, hdPath);
        }
        /// <summary>
        /// Gets the HD path of an account.
        /// </summary>
        /// <param name="coinType">Type of the coin this account is in.</param>
        /// <param name="accountIndex">Index of the account.</param>
        /// <returns>The HD path of an account.</returns>
        public static string GetAccountHdPath(int coinType, int accountIndex, string purpose = null, string hdRootPath = null, string hdAccountPath = null)
        {
            // hdAccountPath ignores everything
            if (hdAccountPath != null)
                return $"{hdAccountPath}";

            // HdRootPath ignores 'coinType' and 'accountIndex'
            if (hdRootPath != null)
                return $"{hdRootPath}/{accountIndex}";

            switch (purpose)
            {
                case null:
                return $"m/84'/{coinType}'/{accountIndex}'";
                case "32":
                case "141":
                return $"m/0'"; // This is a very very very assumption.
                default:
                return $"m/{purpose}'/{coinType}'/{accountIndex}'";
            }
        }

        /// <summary>
        /// Gets the extended key generated by this mnemonic and passphrase.
        /// </summary>
        /// <param name="mnemonic">The mnemonic used to generate the key.</param>
        /// <param name="passphrase">The passphrase used in generating the key.</param>
        /// <returns>The extended key generated by this mnemonic and passphrase.</returns>
        /// <remarks>This key is sometimes referred to as the 'root seed' or the 'master key'.</remarks>
        public static ExtKey GetExtendedKey(string mnemonic, string passphrase = null)
        {
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            return GetExtendedKey(WalletManager.MnemonicFromString(mnemonic), passphrase);
        }

        /// <summary>
        /// Gets the extended key generated by this mnemonic and passphrase.
        /// </summary>
        /// <param name="mnemonic">The mnemonic used to generate the key.</param>
        /// <param name="passphrase">The passphrase used in generating the key.</param>
        /// <returns>The extended key generated by this mnemonic and passphrase.</returns>
        /// <remarks>This key is sometimes referred to as the 'root seed' or the 'master key'.</remarks>
        public static ExtKey GetExtendedKey(Mnemonic mnemonic, string passphrase = null)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));

            return mnemonic.DeriveExtKey(passphrase);
        }

        public static BitcoinExtKey GetWif(ExtKey extKey, string network)
        {
            Guard.NotEmpty(network, nameof(network));

            return GetWif(extKey, GetNetwork(network));
        }

        public static BitcoinExtPubKey GetWif(ExtPubKey extPubKey, Network network)
        {
            return extPubKey.GetWif(network);
        }

        public static BitcoinExtPubKey GetWif(ExtPubKey extPubKey, string network)
        {
            Guard.NotEmpty(network, nameof(network));

            return GetWif(extPubKey, GetNetwork(network));
        }

        public static BitcoinExtKey GetWif(ExtKey extKey, Network network)
        {
            return extKey.GetWif(network);
        }

        /// <summary>
        /// Creates an address' HD path, according to BIP 84.
        /// </summary>
        /// <param name="coinType">Type of coin in the HD path.</param>
        /// <param name="accountIndex">Index of the account in the HD path.</param>
        /// <param name="isChange">A value indicating whether the HD path to generate corresponds to a change address.</param>
        /// <param name="addressIndex">Index of the address in the HD path.</param>
        /// <returns>The HD path.</returns>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        public static string CreateHdPath(int coinType, int accountIndex, bool isChange, int addressIndex, string purpose = "84")
        {
            int change = isChange ? 1 : 0;

            if (purpose == "141" || purpose == "32")
                return $"m/{coinType}'/{change}/{addressIndex}";

            return $"m/{purpose}'/{coinType}'/{accountIndex}'/{change}/{addressIndex}";
        }

        /// <summary>
        /// Gets the type of coin this HD path is for.
        /// </summary>
        /// <param name="hdPath">The HD path.</param>
        /// <returns>The type of coin. <seealso cref="https://github.com/satoshilabs/slips/blob/master/slip-0044.md"/>.</returns>
        /// <exception cref="WalletException">An exception is thrown if the HD path is not well-formed.</exception>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        public static int GetCoinType(string hdPath)
        {
            Guard.NotEmpty(hdPath, nameof(hdPath));

            string[] pathElements = hdPath.Split('/');
            if (pathElements.Length < 3) // Then it's a BIP 32 or BIP 141 hdpath, we asume Bitcoin.
            {
                if (hdPath.StartsWith("m/"))
                {
                    return (int) CoinType.Bitcoin;
                }

                throw new WalletException($"Could not parse CoinType from HdPath {hdPath}.");
            }

            int coinType = 0;
            if (int.TryParse(pathElements[2].Replace("'", string.Empty), out coinType))
            {
                return coinType;
            }

            throw new WalletException($"Could not parse CoinType from HdPath {hdPath}.");
        }

        /// <summary>
        /// Determines whether the HD path corresponds to a change address.
        /// </summary>
        /// <param name="hdPath">The HD path.</param>
        /// <returns>A value indicating if the HD path corresponds to a change address.</returns>
        /// <exception cref="WalletException">An exception is thrown if the HD path is not well-formed.</exception>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        public static bool IsChangeAddress(string hdPath)
        {
            Guard.NotEmpty(hdPath, nameof(hdPath));

            string[] hdPathParts = hdPath.Split('/');
            if (hdPathParts.Length < 5)
                throw new WalletException($"Could not parse value from HdPath {hdPath}.");

            int result = 0;
            if (int.TryParse(hdPathParts[4], out result))
            {
                return result == 1;
            }

            return false;
        }

        /// <summary>
        /// Decrypts the encrypted private key (seed).
        /// </summary>
        /// <param name="encryptedSeed">The encrypted seed to decrypt.</param>
        /// <param name="network">The network this seed applies to.</param>
        /// <param name="password">The password used to decrypt the encrypted seed.</param>
        /// <returns></returns>
        public static Key DecryptSeed(string encryptedSeed, Network network, string password = "")
        {
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(password, nameof(password));

            return Key.Parse(encryptedSeed, password, network);
        }

        /// <summary>
        /// Returns n words back with 1 of the words in the dictionary included from a mnemonic
        /// </summary>
        /// <param name="wordToGuess">The word to hide in the dictionary.</param>
        /// <param name="language">The dictionary language.</param>
        /// <param name="amountAround">The amount of random words around the word to find</param>
        /// <returns></returns>
        public static string[] GenerateGuessWords(string wordToGuess, string language = "english", int amountAround = 9)
        {
            //TODO: Modify method to include two words from mnemonic
            Guard.NotEmpty(wordToGuess, nameof(wordToGuess));
            Guard.NotEmpty(language, nameof(language));

            if (amountAround <= 0)
                throw new WalletException($"It's not allowed to wrap the word around {amountAround} words");

            Random rng = new Random();
            List<string> guessWords = new List<string>(amountAround + 1);
            ReadOnlyCollection<string> dictionaryWordlist;
            dictionaryWordlist = WordlistFromString(language).GetWords();

            int indexOfWord = dictionaryWordlist.IndexOf(wordToGuess);

            // Add the first word
            guessWords.Add(wordToGuess);

            // Add the rest random words
            for (int i = 1; i < amountAround + 1; i++)
            {
                string randomWord = dictionaryWordlist[rng.Next(0, dictionaryWordlist.Count - 1)];
                while (randomWord == wordToGuess)
                {
                    randomWord = dictionaryWordlist[rng.Next(0, dictionaryWordlist.Count - 1)];
                }

                guessWords.Add(randomWord);
            }

            return guessWords.OrderBy(x => (rng.Next(0, amountAround))).ToArray(); // LINQ version
        }

        /// <summary>
        /// Verify if the word is in the mnemonic at the index the user says
        /// </summary>
        /// <param name="mnemonic">A mnemonic words, these are parsed via NBitcoin Mnemonic</param>
        /// <param name="word">A word to find</param>
        /// <param name="index">The index the word is in the mnemonic</param>
        /// <returns></returns>
        public static bool IsInMnemonicAtIndex(string mnemonic, string word, int index)
        {
            Guard.NotEmpty(mnemonic, nameof(mnemonic));
            Mnemonic bitcoinMnemonic = new Mnemonic(mnemonic);

            return IsInMnemonicAtIndex(bitcoinMnemonic, word, index);
        }

        /// <summary>
        /// Verify if the word is in the mnemonic at the index the user says
        /// </summary>
        /// <param name="mnemonic">A mnemonic words, these are parsed via NBitcoin Mnemonic</param>
        /// <param name="word">A word to find</param>
        /// <param name="index">The index the word is in the mnemonic</param>
        /// <returns></returns>
        public static bool IsInMnemonicAtIndex(Mnemonic mnemonic, string word, int index)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));
            Guard.NotEmpty(word, nameof(word));

            if (index < 0)
                throw new WalletException($"An index of {index} is not allowed");

            return mnemonic.Words.ElementAt(index).Equals(word);
        }

        public static Wordlist WordlistFromString(string wordlist = "english")
        {
            switch (wordlist.ToLower())
            {
                case "english":
                    return Wordlist.English;
                case "spanish":
                    return Wordlist.Spanish;
                case "chinese_simplified":
                    return Wordlist.ChineseSimplified;
                case "chinese_traditional":
                    return Wordlist.ChineseTraditional;
                case "french":
                    return Wordlist.French;
                case "japanese":
                    return Wordlist.Japanese;
                case "portuguese_brazil":
                    return Wordlist.PortugueseBrazil;
                default:
                    throw new WalletException($"Invalid wordlist: {wordlist}.\nValid options:\n{String.Join(", ", AcceptedMnemonicWordlists)}");
            }
        }

        public static WordCount WordCountFromInt(int wordCount = 12)
        {
            switch (wordCount)
            {
                case 12:
                    return WordCount.Twelve;
                case 15:
                    return WordCount.Fifteen;
                case 18:
                    return WordCount.Eighteen;
                case 21:
                    return WordCount.TwentyOne;
                case 24:
                    return WordCount.TwentyFour;
                default:
                    throw new WalletException($"Invalid word count: {wordCount}. Only {String.Join(", ", AcceptedMnemonicWordCount)} are valid options.");
            }
        }

        public static ConcurrentChain NewConcurrentChain()
        {
            return new ConcurrentChain();
        }

        /// <summary>
        /// Verify if a word exists in the specified wordlist
        /// </summary>
        /// <param name="word">A word to find</param>
        /// <param name="wordlist">The wordlist to query</param>
        /// <returns></returns>
        public static bool IsWordInWordlist(string word, string wordlist = "english")
        {
            Guard.NotEmpty(word, nameof(word));
            Guard.NotEmpty(wordlist, nameof(wordlist));
            
            return WordlistFromString(wordlist).WordExists(word, out var unused);
        }

        /// <summary>
        /// Verify if a mnemonic has a valid checksum
        /// </summary>
        /// <param name="mnemonic">A mnemonic words, these are parsed via NBitcoin Mnemonic</param>
        /// <param name="wordlist">The wordlist to query</param>
        /// <returns></returns>
        public static bool IsValidChecksum(string mnemonic, string wordlist = "english")
        {
            Guard.NotEmpty(mnemonic, nameof(mnemonic));
            Guard.NotEmpty(wordlist, nameof(wordlist));

            return new Mnemonic(mnemonic, WordlistFromString(wordlist)).IsValidChecksum;
        }

        public static bool IsMnemonicOfWallet(Mnemonic mnemonic, Wallet wallet, Network network = null, string password = null)
        {
            if (wallet == null) return false;
            
            if (network == null) network = Network.Main;
            if (password == null) password = "";

            ExtKey extKeyFromMnemonic = GetExtendedKey(mnemonic, password);
            
            return extKeyFromMnemonic.PrivateKey.GetWif(network).ToString() == wallet.EncryptedSeed;
        }

        public static bool IsMnemonicOfWallet(string mnemonic, Wallet wallet)
        {
            var m = new Mnemonic(mnemonic);

            return IsMnemonicOfWallet(m, wallet);
        }
    }
}