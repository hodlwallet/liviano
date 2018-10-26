using System;
using Liviano;
using CommandLine;
using System.Collections.Generic;

using NBitcoin;

namespace Liviano.CLI
{
    class Program
    {
        public class Options
        {
            [Option('l', "loglevel", Required = false, HelpText = "Set output log level messages.")]
            public string Loglevel { get; set; }

            [Option('h', "help", Required = false, HelpText = "See help")]
            public bool Help { get; set; }

            [Option('t', "testnet", Required = false, HelpText = "Run commands on testnet")]
            public bool Testnet { get; set; }

            [Option('r', "regtest", Required = false, HelpText = "Run commands on regtest")]
            public bool Regtest { get; set; }

        }

        static string HelpContent()
        {
            string content = @"
            Liviano CLI Wallet and SPV Node
            Usage: liviano [runtime-options] [command] [command-options] [arguments]

            Execute a command with Liviano CLI Wallet

            Runtime Options:

            -t|--testnet      Run command on testnet
            -r|--regtest      Run command on regtest
            -h|--help         Show help content
            --version         Display Liviano's version
            -l|--loglevel     Set log level, options: info, error, warning, debug

            Liviano Commands:

            ".Trim();

            return content;
        }

        static bool IsValidLogLevel(string logLevel)
        {
            List<string> validOptions = new List<string>
            {
                "info",
                "error",
                "warning",
                "debug"
            };

            return !validOptions.Contains(logLevel);
        }

        static void Main(string[] args)
        {
            // Set defaults here
            string network = "mainnet";
            string logLevel = "info";

            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                if (o.Loglevel != null)
                {
                    // TODO(igor) Set log level
                    logLevel = o.Loglevel;

                    if (IsValidLogLevel(logLevel))
                    {
                        Console.WriteLine($"Running on log level: {o.Loglevel}");
                    }
                    else
                    {
                        Console.WriteLine(HelpContent());

                        return;
                    }
                }
                else if (o.Help)
                {
                    Console.WriteLine($"{HelpContent()}");

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
                    Console.WriteLine($"{HelpContent()}");

                    // NOTE: Write your test code here, with no limitations.
                    // Set variables like this:
                    // logLevel = "debug";
                    // network = "testnet";

                    Console.WriteLine($"Arguments {args}");

                    Mnemonic mnemo = new Mnemonic(Wordlist.English, WordCount.TwentyFour);
                    ExtKey hdRoot = mnemo.DeriveExtKey();

                    Console.WriteLine(mnemo);

                    return;
                }

                Console.WriteLine($"Running on \"{network}\"");
            });
        }
    }
}
