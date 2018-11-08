using System.Collections.Generic;
using CommandLine;

namespace Liviano.CLI
{
    [Verb("new-mnemonic", HelpText = "Creates a new nmemonic")]
    class NewMnemonicOptions
    {
        [Option('w', "wordlist", HelpText = "Set wordlist, accepted: chinese_simplified, chinese_traditional, english, french, japanese, portuguese_brazil, spanish.")]
        public string Wordlist { get; set; }

        [Option('w', "word-count", HelpText = "Set wordCount, accepted: 12, 15, 18, 21, 24.")]
        public int WordCount { get; set; }
    }

    [Verb("get-ext-key", HelpText = "Get extended key from mnemonic")]
    class GetExtendedKeyOptions
    {
        [Option("mnemonic", HelpText = "Set mnemonic to get ext priv key")]
        public string Mnemonic { get; set; }

        [Option('p', "passphrase", HelpText = "Set passphrase to recover ext priv key")]
        public string Passphrase { get; set; }

        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }
        
        [Option('r', "regtest", HelpText = "Run on regtest")]
        public bool Regtest { get; set; }
    }

    [Verb("get-ext-pub-key", HelpText = "Get extended pubkey key from mnemonic")]
    class GetExtendedPubKeyOptions
    {
        [Option("wif", HelpText = "Set ext priv key to use (wif format)")]
        public string Wif { get; set; }

        [Option("hd-path", HelpText = "Set the HD Path to derive your pub key from")]
        public string HdPath { get; set; }

        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }

        [Option('r', "regtest", HelpText = "Run on regtest")]
        public bool Regtest { get; set; }
    }

    [Verb("derive-address", HelpText = "Derives an address from an extended public key")]
    class DeriveAddressOptions
    {
        [Option("wif", HelpText = "Set ext pub key to use (wif format)")]
        public string Wif { get; set; }

        [Option('c', "is-change", HelpText = "Returns a change address")]
        public bool IsChange { get; set; }

        [Option('i', "index", HelpText = "Index of the address")]
        public int? Index { get; set; }

        [Option("type", HelpText = "Type of the address (p2wpkh, p2sh-p2wpkh and p2pkh")]
        public string Type { get; set; }

        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }

        [Option('r', "regtest", HelpText = "Run on regtest")]
        public bool Regtest { get; set; }
    }

    [Verb("address-to-scriptpubkey", HelpText = "Gets a script pub key from an address")]
    class AddressToScriptPubKeyOptions
    {
        [Option("address", HelpText = "Address to get the script pub key from")]
        public string Address { get; set; }

        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }

        [Option('r', "regtest", HelpText = "Run on regtest")]
        public bool Regtest { get; set; }
    }
}
