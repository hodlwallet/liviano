using System.Collections.Generic;
using CommandLine;

namespace Liviano.CLI
{
    [Verb("mnemonic", HelpText = "Creates a new nmemonic")]
    class MnemonicOptions
    {
        [Option('l', "wordlist", HelpText = "Set wordlist, accepted: chinese_simplified, chinese_traditional, english, french, japanese, portuguese_brazil, spanish.")]
        public string Wordlist { get; set; }

        [Option('c', "word-count", HelpText = "Set wordCount, accepted: 12, 15, 18, 21, 24.")]
        public int WordCount { get; set; }
    }

    [Verb("ext-key", HelpText = "Get extended key from mnemonic")]
    class ExtendedKeyOptions
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

    [Verb("ext-pub-key", HelpText = "Get extended pubkey key from mnemonic")]
    class ExtendedPubKeyOptions
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

    [Verb("new-wallet", HelpText = "Creates a new wallet with a mnemonic")]
    class NewWalletOptions
    {
        [Option("mnemonic", HelpText = "Set mnemonic to create wallet")]
        public string Mnemonic { get; set; }

        [Option("name", HelpText = "Set name to create wallet")]
        public string Name { get; set; }

        [Option('p', "password", HelpText = "Set password to create wallet (get encrypted key)", Required = true)]
        public string Password { get; set; }

        [Option("passphrase", HelpText = "Set passphrase to create wallet (get passphrase)")]
        public string Passphrase { get; set; }

        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }
    }

    [Verb("balance", HelpText = "See the balance on the wallet or by account")]
    class WalletBalanceOptions
    {
        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }

        [Option('p', "password", HelpText = "Password start the wallet", Required = true)]
        public string Password { get; set; }

        [Option("wallet-id", HelpText = "Wallet name to use")]
        public string WalletId { get; set; }

        [Option('n', "name", HelpText = "Account name")]
        public string Name { get; set; }

        [Option('i', "index", HelpText = "Account index")]
        public string Index { get; set; }
    }

    [Verb("new-address", HelpText = "Get a new address of an account or 0 if empty")]
    class NewAddressOptions
    {
        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }

        [Option('p', "password", HelpText = "Password start the wallet", Required = true)]
        public string Password { get; set; }

        [Option("wallet-id", HelpText = "Wallet name to use")]
        public string WalletId { get; set; }

        [Option('n', "name", HelpText = "Account name")]
        public string Name { get; set; }

        [Option('i', "index", HelpText = "Account index")]
        public string Index { get; set; }

        [Option('l', "legacy", HelpText = "Show legacy address")]
        public bool Legacy { get; set; }
    }

    [Verb("send", HelpText = "Send to an address from an account")]
    class SendOptions
    {
        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }

        [Option('p', "password", HelpText = "Password start the wallet", Required = true)]
        public string Password { get; set; }

        [Option("wallet-id", HelpText = "Wallet name to use")]
        public string WalletId { get; set; }

        [Option('n', "name", HelpText = "Account name")]
        public string Name { get; set; }

        [Option('i', "index", HelpText = "Account index")]
        public string Index { get; set; }

        [Option("to", HelpText = "Address to send Bitcoin to", Required = true)]
        public string To { get; set; }

        [Option("amount", HelpText = "Amount in BTC to send", Required = true)]
        public double Amount { get; set; }

        [Option("sats-per-byte", HelpText = "Fees on sats per byte", Required = true)]
        public int SatsPerByte { get; set; }
    }

    [Verb("start", HelpText = "Starts the SPV node and sync loaded wallet")]
    class StartOptions
    {
        [Option('t', "testnet", HelpText = "Run on testnet")]
        public bool Testnet { get; set; }

        [Option('p', "password", HelpText = "Password start the wallet", Required = true)]
        public string Password { get; set; }

        [Option("wallet-id", HelpText = "Wallet name to use")]
        public string WalletId { get; set; }

        [Option('n', "nodes-to-connect", HelpText = "Number of nodes to connect")]
        public int NodesToConnect { get;  set; }

        [Option('d', "date", HelpText = "Date to start on")]
        public string DateTime { get; set; }
    }
}
