using System;
using System.Diagnostics;
using System.Collections.Generic;

using Mono.Options;
using NBitcoin;
using Serilog;

using Liviano.Bips;

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
            var passphrase = "";
            var mnemonic = "";
            var wordlist = "english";
            var wordCount = 12;
            var hasInputText = Console.IsInputRedirected;
            var inputText = hasInputText && !Debugger.IsAttached ? Console.ReadLine().Trim() : "";
            var wif = "";
            var address = "";
            var addressType = "p2wpkh";
            var hdPath = "m/84'/0'/0'/0/0"; // Default BIP84 / Bitcoin / 1st account / receive / 1st pubkey
            var server = "locahost:s50001";
            var amount = new Decimal(0.00);
            var accountIndex = 0;
            var walletId = "";

            // Menu show
            var showHelp = false;
            var getXPrv = false;
            var getXPub = false;
            var getAddr = false;
            var newMnemonic = false;
            var newWallet = false;
            var getScriptPubKey = false;
            var send = false;
            var balance = false;
            var newAcc = false;

            // Debug menu item
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
                {"nw|new-wallet", "Create a new wallet", v => newWallet = !(v is null)},
                {"send|send-to-address", "Send to address", v => send = !(v is null)},
                {"bal|balance", "Show wallet balance", v => balance = !(v is null)},
                {"new-acc|new-account", "Create a new account on the wallet", v => newAcc = !(v is null)},

                // Variables or modifiers
                {"l|lang=", "Mnemonic language", (string v) => wordlist = v},
                {"wc|word-count=", "Mnemonic word count", (int v) => wordCount = v},
                {"type|address-type=", "Set address type", (string v) => addressType = v},
                {"hdpath|with-hd-path=", "Set hd path type", (string v) => hdPath = v},
                {"pass|passphrase=", "Passphrase", (string v) => passphrase = v},
                {"s|server=", "Server", (string v) => server = v},
                {"amt|amount=", "Amount to send", (string v) => amount = Decimal.Parse(v)},
                {"acc|account=", "Account to send from", (string v) => accountIndex = int.Parse(v)},
                {"w|wallet=", "Wallet id", (string v) => walletId = v},

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
                InvalidArguments($"Error: {e.Message}");

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
                var mnemonicRes = Hd.NewMnemonic(wordlist, wordCount);

                Console.WriteLine(mnemonicRes);

                return;
            }

            if (getXPrv)
            {
                if (string.IsNullOrEmpty(mnemonic))
                {
                    InvalidArguments();

                    return;
                }

                var extKey = Hd.GetExtendedKey(mnemonic, passphrase);
                var wifRes = Hd.GetWif(extKey, network);

                Console.WriteLine(wifRes);

                return;
            }

            if (getXPub)
            {
                if (string.IsNullOrEmpty(wif))
                {
                    InvalidArguments();

                    return;
                }

                var extPubKey = Hd.GetExtendedPublicKey(wif, hdPath, network.Name);
                var extPubKeyWif = Hd.GetWif(extPubKey, network);

                Console.WriteLine(extPubKeyWif);

                return;
            }

            if (getAddr)
            {
                int index = 1;
                bool isChange = false;

                if (string.IsNullOrEmpty(wif))
                {
                    InvalidArguments();

                    return;
                }

                Console.WriteLine(Hd.GetAddress(wif, index, isChange, network.Name, addressType));

                return;
            }

            if (getScriptPubKey)
            {
                if (string.IsNullOrEmpty(address))
                {
                    InvalidArguments();

                    return;
                }

                Console.WriteLine(Hd.GetScriptPubKey(address, network.Name));

                return;
            }

            if (newWallet)
            {
                Wallet wallet;
                if (string.IsNullOrEmpty(mnemonic))
                {
                    wallet = LightClient.NewWallet(wordlist, wordCount, network);
                }
                else
                {
                    wallet = LightClient.NewWalletFromMnemonic(mnemonic, network);
                }

                wallet.Storage.Save();

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
                LightClient.WalletTest1(network, wordlist, wordCount);

                return;
            }

            // End... invalid options
            InvalidArguments();

            // TODO Missing functionality
            // - Send from a wallet
            // - Start wallet and listen
            // - Get wallet address from account
            // - Get wallet balance for all accounts or one
            // - Create account on a wallet
        }

        static void InvalidArguments(string msg = "Invalid argument options.")
        {
            Console.WriteLine($"{msg}\n");
            LightClient.ShowHelp();
        }
    }
}
