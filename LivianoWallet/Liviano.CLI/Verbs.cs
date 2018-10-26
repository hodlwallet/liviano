using CommandLine;
using Liviano;

namespace Liviano.CLI
{
    [Verb("new-mnemonic", HelpText = "Creates a new nmemonic")]
    class NewMnemonicOptions
    {
        [Option('w', "wordlist", Required = false, HelpText = "Set wordlist, accepted: ChineseSimplified, ChineseTraditional, English, French, Japanese, PortugueseBrazil, Spanish.")]
        public string wordlist { get; set; }

        [Option('w', "word-count", Required = false, HelpText = "Set wordCount, accepted: 12, 15, 18, 21, 24.")]
        public int wordCount { get; set; }
    }
}
