using System.Collections.Generic;
using CommandLine;

namespace Liviano.CLI
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
        public static bool IsValidLogLevel(string logLevel)
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

        public static string HelpContent()
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
    }
}
