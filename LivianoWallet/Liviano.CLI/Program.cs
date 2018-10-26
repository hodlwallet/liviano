using System;
using System.Collections.Generic;
using CommandLine;

using Liviano;

// Testing imports...
// NOTE remove later.
using NBitcoin;

namespace Liviano.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            // Set defaults here
            string network = "mainnet";
            string logLevel = "info";

            Parser.Default.ParseArguments<Options, NewMnemonicOptions, GetExtendedKeyOptions>(args)
            .WithParsed<Options>(o =>
            {
                if (o.Loglevel != null)
                {
                    // TODO(igor) Set log level
                    logLevel = o.Loglevel;

                    if (Options.IsValidLogLevel(logLevel))
                    {
                        Console.WriteLine($"Running on log level: {o.Loglevel}");
                    }
                    else
                    {
                        Console.WriteLine(Options.HelpContent());

                        return;
                    }
                }
                else if (o.Help)
                {
                    Console.WriteLine($"{Options.HelpContent()}");

                    return;
                }
                else if (o.Testnet)
                {
                    network = "testnet";
                }
                else if (o.Regtest)
                {
                    network = "regtest";
                }
                else
                {
                    Console.WriteLine($"{Options.HelpContent()}");

                    return;
                }

                Console.WriteLine($"Running on \"{network}\"");
            })
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

                if (o.Mnemonic != null)
                {
                    mnemonic = o.Mnemonic;
                }

                if (o.Passphrase != null)
                {
                    passphrase = o.Passphrase;
                }

                if (mnemonic == null)
                {
                    mnemonic = Console.ReadLine();
                }

                var extKey = HdOperations.GetExtendedKey(mnemonic, passphrase);
                var wif = HdOperations.GetWif(extKey, network);

                Console.WriteLine(wif.ToString());
            });
        }
    }
}
