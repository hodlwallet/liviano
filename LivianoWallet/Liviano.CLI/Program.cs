using System;
using System.Collections.Generic;
using CommandLine;

using Liviano;

namespace Liviano.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<NewMnemonicOptions, GetExtendedKeyOptions, GetExtendedPubKeyOptions>(args)
            .WithParsed<NewMnemonicOptions>(o => {
                string wordlist = "english";
                int wordCount = 24;

                if (o.WordCount != 0)
                {
                    wordCount = o.WordCount;
                }

                if (o.Wordlist != null)
                {
                    wordlist = o.Wordlist;
                }

                Console.WriteLine(WalletManager.NewMnemonic(wordlist, wordCount).ToString());
            })
            .WithParsed<GetExtendedKeyOptions>(o => {
                string mnemonic = null;
                string passphrase = null;
                string network = "main";

                if (o.Mnemonic != null)
                {
                    mnemonic = o.Mnemonic;
                }
                else
                {
                    mnemonic = Console.ReadLine();
                }

                if (o.Passphrase != null)
                {
                    passphrase = o.Passphrase;
                }

                if (o.Testnet)
                {
                    network = "testnet";
                }
                else if (o.Regtest)
                {
                    network = "regtest";
                }

                var extKey = HdOperations.GetExtendedKey(mnemonic, passphrase);
                var wif = HdOperations.GetWif(extKey, network);

                Console.WriteLine(wif.ToString());
            })
            .WithParsed<GetExtendedPubKeyOptions>(o => {
                string wif = null;
                string hdPath = o.HdPath;
                string network = "main";

                if (o.Wif != null)
                {
                    wif = o.Wif;
                }
                else
                {
                    wif = Console.ReadLine();
                }

                if (o.Testnet)
                {
                    network = "testnet";
                }
                else if (o.Regtest)
                {
                    network = "regtest";
                }

                var extPubKey = HdOperations.GetExtendedPublicKey(wif, hdPath, network);
                var extPubKeyWif = HdOperations.GetWif(extPubKey, network);

                Console.WriteLine(extPubKeyWif.ToString());
            });
        }
    }
}
