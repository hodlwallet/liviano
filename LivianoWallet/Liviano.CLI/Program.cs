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
            Parser.Default.ParseArguments<NewMnemonicOptions, GetExtendedKeyOptions, GetExtendedPubKeyOptions, DeriveAddressOptions, AddressToScriptPubKeyOptions>(args)
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
                string wif;
                string hdPath = "m/44'/0'/0'"; // Default BIP44 / Bitcoin / 1st account
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

                if (o.HdPath != null)
                {
                    hdPath = o.HdPath;
                }

                var extPubKey = HdOperations.GetExtendedPublicKey(wif, hdPath, network);
                var extPubKeyWif = HdOperations.GetWif(extPubKey, network);

                Console.WriteLine(extPubKeyWif.ToString());
            })
            .WithParsed<DeriveAddressOptions>(o => {
                string wif;
                int index = 1;
                bool isChange = false;
                string network = "main";
                string type = "p2wpkh";

                if (o.Wif != null)
                {
                    wif = o.Wif;
                }
                else
                {
                    wif = Console.ReadLine();
                }

                if (o.Index != null)
                {
                    index = (int) o.Index;
                }

                if (o.IsChange)
                {
                    isChange = o.IsChange;
                }

                if (o.Testnet)
                {
                    network = "testnet";
                }
                else if (o.Regtest)
                {
                    network = "regtest";
                }

                if (o.Type != null)
                {
                    type = o.Type;
                }

                Console.WriteLine(HdOperations.GetAddress(wif, index, isChange, network, type).ToString());
            })
            .WithParsed<AddressToScriptPubKeyOptions>(o => {
                string network = "main";
                string address = null;

                if (o.Testnet)
                {
                    network = "testnet";
                }
                else if (o.Regtest)
                {
                    network = "regtest";
                }

                if (o.Address != null)
                {
                    address = o.Address;
                }
                else
                {
                    address = Console.ReadLine();
                }

                Console.WriteLine(HdOperations.GetScriptPubKey(address, network).ToString());
            });
        }
    }
}
