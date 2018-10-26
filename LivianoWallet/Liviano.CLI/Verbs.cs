using CommandLine;
using Liviano;

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

        [Option("passphrase", Required = false, HelpText = "Set passphrase to recover ext priv key")]
        public string Passphrase { get; set; }
    }
}
