using System.Collections.Generic;
using CommandLine;

namespace Liviano.CLI
{
    [Verb("new-mnemonic", HelpText = "Creates a new nmemonic")]
    class NewMnemonicOptions
    {
        [Option('w', "wordlist", Required = false, HelpText = "Set wordlist, accepted: ChineseSimplified, ChineseTraditional, English, French, Japanese, PortugueseBrazil, Spanish.")]
        public string Wordlist { get; set; }

        [Option('w', "word-count", Required = false, HelpText = "Set wordCount, accepted: 12, 15, 18, 21, 24.")]
        public int WordCount { get; set; }
    }

    [Verb("get-ext-key", HelpText = "Get extended key from mnemonic")]
    class GetExtendedKeyOptions
    {
        [Option("mnemonic", Required = false, HelpText = "Set mnemonic to get ext priv key")]
        public string Mnemonic { get; set; }

        [Option('p', "passphrase", Required = false, HelpText = "Set passphrase to recover ext priv key")]
        public string Passphrase { get; set; }

        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }
        
        [Option('r', "regtest", HelpText = "Run on regnet")]
        public bool Regtest { get; set; }
    }

    [Verb("get-ext-pub-key", HelpText = "Get extended pubkey key from mnemonic")]
    class GetExtendedPubKeyOptions
    {
        [Option("wif", Required = false, HelpText = "Set ext priv key to use (wif format)")]
        public string Wif { get; set; }

        [Option("hd-path", Required = true, HelpText = "Set the HD Path to derive your pub key from")]
        public string HdPath { get; set; }

        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }

        [Option('r', "regtest", HelpText = "Run on regnet")]
        public bool Regtest { get; set; }
    }
}
