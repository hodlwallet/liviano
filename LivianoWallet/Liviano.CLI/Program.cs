using System;
using System.Text;
using System.Collections.Generic;

using Mono.Options;
using NBitcoin;
using Serilog;

using Liviano.Bips;
using System.Diagnostics;

namespace Liviano.CLI
{
    class Program
    {
        static ILogger logger;

        static void Main(string[] args)
        {
            logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            // Defaults
            var network = Network.Main;
            var mnemonic = "";
            var mnemonicLang = "english";
            var passphrase = "";
            var mnemonicWordCount = 12;
            var hasInputText = Console.IsInputRedirected;
            var inputText = hasInputText && !Debugger.IsAttached ? Console.ReadLine().Trim() : "";
            var wif = "";
            var address = "";
            var addressType = "p2wpkh";
            var hdPath = "m/84'/0'/0'/0/0"; // Default BIP84 / Bitcoin / 1st account / receive / 1st pubkey

            // Menu show
            var showHelp = false;
            var getXPrv = false;
            var getXPub = false;
            var getAddr = false;
            var newMnemonic = false;
            var getScriptPubKey = false;
            var electrumTest3 = false;
            var walletTest1 = false;

            // Define options
            var options = new OptionSet
            {
                // Global variables
                {"m|mainnet", "Run on mainnet", v => network = !(v is null) ? Network.Main : Network.TestNet},
                {"t|testnet", "Run on testnet", v => network = !(v is null) ? Network.TestNet : Network.Main},

                // Actions
                {"xprv|ext-priv-key", "Get an xpriv from mnemonic", v => getXPrv = !(v is null)},
                {"xpub|ext-pub-key", "Get an xpub from a xprv", v => getXPub = !(v is null)},
                {"getaddr|get-address", "Get an address from a xpub", v => getAddr= !(v is null)},
                {"nmn|new-mnemonic", "Get new mnemonic", v => newMnemonic = !(v is null)},
                {"to-scriptpubkey|address-to-script-pub-key", "Get script pub key from address", v => getScriptPubKey = !(v is null)},

                // Variables or modifiers
                {"l|lang=", "Mnemonic language", (string v) => mnemonicLang = v},
                {"wc|word-count=", "Mnemonic word count", (int v) => mnemonicWordCount = v},
                {"type|address-type=", "Set address type", (string v) => addressType = v},
                {"hdpath|with-hd-path=", "Set hd path type", (string v) => hdPath = v},
                {"pass|passphrase=", "Passphrase", (string v) => passphrase = v},

                // Default & help
                {"h|help", "Liviano help", v => showHelp = !(v is null)},

                // Debugging commands
                {"test-et3|electrum-test-3", "Electrum test 3", v => electrumTest3 = !(v is null)},
                {"test-w1|wallet-test-1", "Test wallet 1", v => walletTest1 = !(v is null)}
            };

            // Variables that support input text from the terminal
            if (hasInputText)
            {
                mnemonic = inputText;
                address = inputText;
                wif = inputText;
            }
            else
            {
                options.Add("mn|mnemonic=", "Mnemonic", (string v) => mnemonic = v);
                options.Add("addr|address=", "Address", (string v) => address = v);
                options.Add("wif|with-wif=", "Wif", (string v) => wif = v);
            }

            // Parse arguments
            List<string> extra;
            try
            {
                extra = options.Parse(args);

                if (extra.Count > 0)
                    logger.Information($"Extra arguments: {extra}");
            }
            catch (OptionException e)
            {
                Console.WriteLine($"Error: {e.Message}");

                LightClient.ShowHelp();

                return;
            }

            // Check if help was sent
            if (showHelp)
            {
                LightClient.ShowHelp();

                return;
            }

            // LightClient commands, set everything before here
            if (newMnemonic)
            {
                var mnemonicRes = Hd.NewMnemonic(mnemonicLang, mnemonicWordCount);

                Console.WriteLine(mnemonicRes);

                return;
            }

            if (getXPrv)
            {
                var extKey = Hd.GetExtendedKey(mnemonic, passphrase);
                var wifRes = Hd.GetWif(extKey, network);

                Console.WriteLine(wifRes);

                return;
            }

            if (getXPub)
            {
                var extPubKey = Hd.GetExtendedPublicKey(wif, hdPath, network.Name);
                var extPubKeyWif = Hd.GetWif(extPubKey, network);

                Console.WriteLine(extPubKeyWif);

                return;
            }

            if (getAddr)
            {
                int index = 1;
                bool isChange = false;

                Console.WriteLine(Hd.GetAddress(wif, index, isChange, network.Name, addressType));

                return;
            }

            if (getScriptPubKey)
            {
                Console.WriteLine(Hd.GetScriptPubKey(address, network.Name));

                return;
            }

            // Test / debugging LightClient commands
            if (electrumTest3)
            {
                LightClient.TestElectrumConnection3(network);

                return;
            }

            if (walletTest1)
            {
                Console.WriteLine("Bill gates is corona");

                return;
            }

            // End... invalid options
            Console.WriteLine("Invalid argument optoins.\n");
            LightClient.ShowHelp();

            // TODO Missing functionality
            // - Create new wallet
            // - Send from a wallet
            // - Start wallet and listen
            // - Get wallet balance
            // - Create account on wallet
        }
    }
}
