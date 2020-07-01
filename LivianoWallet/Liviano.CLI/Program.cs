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
        static Config config;
        static OptionSet options;

        public const string DEFAULT_ACCOUNT_NAME = "Main Account";
        public const string DEFAULT_WALLET_NAME = "Bitcoin Wallet";

        // Defaults options values
        static Network network = Network.Main;
        static string passphrase = "";
        static string mnemonic = "";
        static string wordlist = "english";
        static int wordCount = 12;
        static bool hasInputText = false;
        static string inputText = "";
        static string wif = "";
        static string address = "";
        static string addressType = "p2wpkh";
        static string hdPath = "m/84'/0'/0'/0/0"; // Default BIP84 / Bitcoin / 1st account / receive / 1st pubkey
        static string server = "locahost:s50001";
        static decimal amount = new decimal(0.00);
        static int accountIndex = 0;
        static string walletId = "";
        static string walletName = DEFAULT_WALLET_NAME;
        static string accName = DEFAULT_ACCOUNT_NAME;
        static string accType = "bip84";

        // Menu of the cli program
        static bool showHelp = false;
        static bool getXPrv = false;
        static bool getXPub = false;
        static bool getAddr = false;
        static bool newMnemonic = false;
        static bool newWallet = false;
        static bool getScriptPubKey = false;
        static bool send = false;
        static bool balance = false;
        static bool newAcc = false;
        static bool start = false;

        // Parse extra options arguments
        static List<string> extra;

        // Debug menu items for cli program
        static bool electrumTest3 = false;
        static bool walletTest1 = false;

        /// <summary>
        /// Defines all the options that we need for the CLI
        /// </summary>
        /// <returns></returns>
        static OptionSet GetOptions()
        {
            return new OptionSet
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
                {"st|start", "Start wallet sync, and wait for transactions", v => start = !(v is null)},

                // Variables or modifiers
                {"l|lang=", "Mnemonic language", (string v) => wordlist = v},
                {"wc|word-count=", "Mnemonic word count", (int v) => wordCount = v},
                {"type|address-type=", "Set address type", (string v) => addressType = v},
                {"hdpath|with-hd-path=", "Set hd path type", (string v) => hdPath = v},
                {"pass|passphrase=", "Passphrase", (string v) => passphrase = v},
                {"s|server=", "Server", (string v) => server = v},
                {"amt|amount=", "Amount to send", (string v) => amount = decimal.Parse(v)},
                {"acc|account=", "Account to send from", (string v) => accountIndex = int.Parse(v)},
                {"accname|account-name=", "Account name", (string v) => accName = v},
                {"acctype|account-type=", "Account type", (string v) => accType = v},
                {"w|wallet=", "Wallet id", (string v) => walletId = v},
                {"wn|wallet-name=", "Wallet name", (string v) => walletName = v},

                // Default & help
                {"h|help", "Liviano help", v => showHelp = !(v is null)},

                // Debugging commands
                {"test-et3|electrum-test-3", "Electrum test 3", v => electrumTest3 = !(v is null)},
                {"test-w1|wallet-test-1", "Test wallet 1", v => walletTest1 = !(v is null)}
            };
        }

        static void Main(string[] args)
        {
            logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            // Set standard input
            hasInputText = Console.IsInputRedirected;
            if (hasInputText && !Debugger.IsAttached) inputText = Console.ReadLine().Trim();

            // Get the options
            options = GetOptions();

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
                ShowHelp(options);

                return;
            }

            // Load configs
            if (Config.Exists())
                config = Config.Load();
            else
                config = new Config(walletId, network.ToString());

            if (config.HasWallet(walletId) && walletId is null)
                walletId = config.WalletId;

            if (!string.IsNullOrEmpty(config.Network) && network is null)
                network = Hd.GetNetwork(config.Network);

            config.Network = network.ToString();
            config.AddWallet(walletId);

            config.SaveChanges();

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
                if (string.IsNullOrEmpty(wif) && string.IsNullOrEmpty(walletId))
                {
                    InvalidArguments();

                    return;
                }

                if (!string.IsNullOrEmpty(wif))
                    Console.WriteLine(Hd.GetAddress(wif, accountIndex, false, network.Name, addressType));
                else if (config.HasWallet(walletId))
                {
                    var wallet = config.WalletId;
                }

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
                    wallet = LightClient.NewWallet(wordlist, wordCount, network);
                else
                    wallet = LightClient.NewWalletFromMnemonic(mnemonic, network);

                if (!string.IsNullOrEmpty(accName) && !string.IsNullOrEmpty(accType))
                    wallet.AddAccount(accType, accName, new { Wallet = wallet, WalletId = wallet.Id, Network = network });

                wallet.Storage.Save();

                // Save wallet on config
                config.AddWallet(wallet.Id);
                config.SaveChanges();

                Console.WriteLine($"{wallet.Id}");

                return;
            }

            if (newAcc)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                    throw new ArgumentException("New account needs a wallet id");
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                if (string.IsNullOrEmpty(accName) || string.IsNullOrEmpty(accType))
                    throw new ArgumentException("New account needs a account type and account name");

                var res = LightClient.AddAccount(config, accType, accName);

                // Returns wif of account
                Console.WriteLine($"{res}");

                return;
            }

            if (start)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                    throw new ArgumentException("New account needs a wallet id");
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);


                LightClient.Start(config, resync: false);

                return;
            }

            if (send)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                    throw new ArgumentException("New account needs a wallet id");
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                // TODO Send from an account

                throw new NotImplementedException("Send is not implemented");
            }

            if (balance)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                    throw new ArgumentException("New account needs a wallet id");
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                // TODO Get balance from an account or from all of them in the wallet

                throw new NotImplementedException("Balance is not implemented");
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
        }

        static void ShowHelp(OptionSet options)
        {
            // show some app description message
            Console.WriteLine("Usage: ./liviano-cli [OPTIONS]");
            Console.WriteLine("CLI version of Liviano.");
            Console.WriteLine("Can be used as an example for a Wallet or as an utility for Bitcoin");
            Console.WriteLine();

            // output the options
            Console.WriteLine("Options:");
            options.WriteOptionDescriptions(Console.Out);
        }


        static void InvalidArguments(string msg = "Invalid argument options.")
        {
            Console.WriteLine($"{msg}\n");

            ShowHelp(options);
        }
    }
}
