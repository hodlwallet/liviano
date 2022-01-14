//
// Program.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

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
        static Network network = null;
        static string passphrase = "";
        static string mnemonic = "";
        static string wordlist = "english";
        static int wordCount = 12;
        static int addressAmount = 1;
        static bool hasInputText = false;
        static string inputText = "";
        static string wif = "";
        static string address = "";
        static string addressType = "p2wpkh";
        static string hdPath = "m/84'/0'/0'/0/0"; // Default BIP84 / Bitcoin / 1st account / receive / 1st pubkey
        // static string server = "";
        static double amount = 0.00;
        static decimal feeSatsPerByte = 1m;
        static long dustAmount = -1;
        static int accountIndex = -1;
        // static string accountName = null;
        static string walletId = "";
        // static string walletName = DEFAULT_WALLET_NAME;
        static string newAccName = DEFAULT_ACCOUNT_NAME;
        static string newAccType = "bip84";
        static string txId = null;

        // Menu of the cli program
        static bool showHelp = false;
        static bool getXPrv = false;
        static bool getXPub = false;
        static bool getAddr = false;
        static bool newMnemonic = false;
        static bool newWallet = false;
        static bool getScriptPubKey = false;
        static bool send = false;
        static bool bump = false;
        static bool balance = false;
        static bool summary = false;
        static bool newAcc = false;
        static bool infoAcc = false;
        static bool start = false;
        static bool resync = false;
        static bool sync = false;
        static bool ping = false;
        static bool headers = false;
        static bool coinControl = false;
        static bool dustControl = false;
        static string freezeCoin = null;
        static string unfreezeCoin = null;
        static bool version = false;
        static bool refreshAddress = false;
        static bool detectAccount = false;
        static bool saveAccounts = false;
        static bool testStuff = false;

        // Parse extra options arguments
        static List<string> extra;

        /// <summary>
        /// Defines all the options that we need for the CLI
        /// </summary>
        /// <remarks>Sets a lot of static variables on this class</remarks>
        /// <returns>A <see cref="OptionSet"/> of the CLI option</returns>
        static OptionSet GetOptions()
        {
            return new OptionSet
            {
                // Global variables
                {"m|mainnet", "Run on mainnet", v => network = v is not null ? Network.Main : null},
                {"t|testnet", "Run on testnet", v => network = v is not null ? Network.TestNet : null},

                // Actions
                {"xprv|ext-priv-key", "Get an xpriv from mnemonic", v => getXPrv = v is not null},
                {"xpub|ext-pub-key", "Get an xpub from a xprv", v => getXPub = v is not null},
                {"getaddr|get-address", "Get an address from a xpub", v => getAddr= v is not null},
                {"nmn|new-mnemonic", "Get new mnemonic", v => newMnemonic = v is not null},
                {"to-scriptpubkey|address-to-script-pub-key", "Get script pub key from address", v => getScriptPubKey = v is not null},
                {"nw|new-wallet", "Create a new wallet", v => newWallet = v is not null},
                {"send|send-to-address", "Send to address", v => send = v is not null},
                {"bump|bump-fee", "Bump fee of a transaction", v => bump = v is not null},
                {"bal|balance", "Show wallet balance", v => balance = v is not null},
                {"s|summary", "Show wallet summary", v => summary = v is not null},
                {"new-acc|new-account", "Create a new account on the wallet", v => newAcc = v is not null},
                {"info-acc|info-account", "Gets the account info", v => infoAcc = v is not null},
                {"st|start", "Start wallet sync, and wait for transactions", v => start = v is not null},
                {"rs|resync", "Start wallet resync and exit when done", v => resync = v is not null},
                {"sy|sync", "Start wallet sync and exit when done", v => sync = v is not null},
                {"cc|coin-control", "Show the coins / froze / unfreeze coins", v => coinControl = v is not null},
                {"dc|dust-control", "Update dust control and sets a new value for it", v => dustControl = v is not null},
                {"v|version", "Show the liviano version", v => version = v is not null},
                {"dccs|detect-accounts", "Detect accounts on a mnemonic seed", v => detectAccount = v is not null},
                {"sdccs|save-accounts", "Save detected accounts on a mnemonic seed", v => saveAccounts = v is not null},
                {"tst|test", "Test stuff", v => testStuff = v is not null},

                // Variables or modifiers
                {"l|lang=", "Mnemonic language", (string v) => wordlist = v},
                {"wc|word-count=", "Mnemonic word count", (int v) => wordCount = v},
                {"type|address-type=", "Set address type", (string v) => addressType = v},
                {"hdpath|with-hd-path=", "Set hd path type", (string v) => hdPath = v},
                {"pass|passphrase=", "Passphrase", (string v) => passphrase = v},
                // {"s|server=", "Server", (string v) => server = v},
                {"amt|amount=", "Amount to send", (string v) => amount = double.Parse(v)},
                {"fee|fee-sats-per-byte=", "Fees in satoshis per byte", (string v) => feeSatsPerByte = decimal.Parse(v)},
                {"acci|account-index=", "Account to send from", (string v) => accountIndex = int.Parse(v)},
                // {"accn|account-name=", "Account to send from", (string v) => accountName = v},
                {"naccname|new-account-name=", "New account name", (string v) => newAccName = v},
                {"nacctype|new-account-type=", "New account type", (string v) => newAccType = v},
                {"w|wallet=", "Wallet id", (string v) => walletId = v},
                // {"wn|wallet-name=", "Wallet name", (string v) => walletName = v},
                {"addramt|address-amount=", "Amount of addresses to generate", (int v) => addressAmount = v},
                {"fc|freeze-coin=", "TxId:N of the coin to freeze", (string v) => freezeCoin = v},
                {"ufc|unfreeze-coin=", "TxId:N of the coin to unfreeze", (string v) => unfreezeCoin = v},
                {"da|dust-amount=", "Amount to set a dust thereshold", (string v) => dustAmount = long.Parse(v)},
                {"txid|tx-id=", "TxId of the tx to bump fee", (string v) => txId = v},
                {"png|ping", "Ping electrum server", (string v) => ping = v is not null},
                {"hdr|headers", "Subscribe to new headers", (string v) => headers = v is not null},
                {"raddr|refresh-address", "Refresh address", v => refreshAddress = v is not null},

                // Default & help
                {"h|help", "Liviano help", v => showHelp = v is not null}
            };
        }

        /// <summary>
        /// Main, uses the args and run the options
        /// </summary>
        static int Main(string[] args)
        {
            logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            // Set standard input
            hasInputText = Console.IsInputRedirected && !Debugger.IsAttached;
            if (hasInputText) inputText = Console.ReadLine().Trim();
            if (string.IsNullOrEmpty(inputText)) hasInputText = false;

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
                    logger.Information("Extra arguments: {0}", string.Join(", ", extra));
            }
            catch (OptionException e)
            {
                InvalidArguments($"Error: {e.Message}");

                return 0;
            }

            // Check if help was sent
            if (showHelp)
            {
                ShowHelp(options);

                return 0;
            }

            if (version)
            {
                Console.WriteLine(Liviano.Version.ToString());

                return 0;
            }

            // Load configs
            if (Config.Exists())
                config = Config.Load();
            else
            {
                config = new Config(
                    walletId,
                    network is null ? Network.Main.ToString() : network.ToString()
                );
            }

            if (config.HasWallet(walletId) && walletId is null) walletId = config.WalletId;

            if (!string.IsNullOrEmpty(config.Network) && network is null)
                network = Hd.GetNetwork(config.Network);

            config.Network = network.ToString();

            if (!string.IsNullOrEmpty(walletId)) config.AddWallet(walletId);

            config.SaveChanges();

            // LightClient commands, set everything before here
            if (newMnemonic)
            {
                var mnemonicRes = Hd.NewMnemonic(wordlist, wordCount);

                Console.WriteLine(mnemonicRes);

                return 0;
            }

            if (getXPrv)
            {
                if (string.IsNullOrEmpty(mnemonic))
                {
                    InvalidArguments();

                    return 1;
                }

                var extKey = Hd.GetExtendedKey(mnemonic, passphrase);
                var wifRes = Hd.GetWif(extKey, network);

                Console.WriteLine(wifRes);

                return 0;
            }

            if (getXPub)
            {
                if (string.IsNullOrEmpty(wif))
                {
                    InvalidArguments();

                    return 1;
                }

                var extPubKey = Hd.GetExtendedPublicKey(wif, hdPath, network.Name);
                var extPubKeyWif = Hd.GetWif(extPubKey, network);

                Console.WriteLine(extPubKeyWif);

                return 0;
            }

            if (getAddr)
            {
                if (string.IsNullOrEmpty(wif) && string.IsNullOrEmpty(walletId) && config.WalletId == null)
                {
                    InvalidArguments();

                    return 1;
                }

                if (accountIndex == -1) accountIndex = 0;

                if (!string.IsNullOrEmpty(wif))
                    Console.WriteLine(Hd.GetAddress(wif, accountIndex, false, network.Name, addressType));
                else if (config.WalletId != null)
                {
                    if (addressAmount == 1)
                    {
                        var address = LightClient.GetAddress(config, accountIndex, refreshAddress);

                        if (address is null)
                        {
                            InvalidArguments("Could not get address because wallet was not found");

                            return 1;
                        }


                        Console.WriteLine(address.ToString());

                        return 0;
                    }

                    var addresses = LightClient.GetAddresses(config, accountIndex, addressAmount, refreshAddress: refreshAddress);

                    var data = string.Join('\n', addresses.ToList());

                    Console.WriteLine(data);

                    return 0;
                }
                else
                {
                    InvalidArguments("Could not find wallet or wif was not provided");

                    return 1;
                }

                return 0;
            }

            if (getScriptPubKey)
            {
                if (string.IsNullOrEmpty(address))
                {
                    InvalidArguments();

                    return 1;
                }

                Console.WriteLine(Hd.GetScriptPubKey(address, network.Name));

                return 0;
            }

            if (newWallet)
            {
                Wallet wallet;
                if (string.IsNullOrEmpty(mnemonic))
                    wallet = LightClient.NewWallet(wordlist, wordCount, passphrase, network);
                else
                    wallet = LightClient.NewWalletFromMnemonic(mnemonic, passphrase, network);

                if (!string.IsNullOrEmpty(newAccName) && !string.IsNullOrEmpty(newAccType))
                    wallet.AddAccount(newAccType, newAccName, new { Wallet = wallet, WalletId = wallet.Id, Network = network });

                wallet.Storage.Save();

                // Save wallet on config
                config.AddWallet(wallet.Id);
                config.SaveChanges();

                Console.WriteLine($"{wallet.Id}");

                return 0;
            }

            if (newAcc)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("New account needs a wallet id");

                    return 1;
                }
                else
                {
                    logger.Information("Using wallet id: {walletId}", config.WalletId);
                }

                if (string.IsNullOrEmpty(newAccName) || string.IsNullOrEmpty(newAccType))
                {
                    Console.WriteLine("New account needs a account type and account name");

                    return 1;
                }

                var res = LightClient.AddAccount(config, newAccType, newAccName);

                // Returns wif of account
                Console.WriteLine($"{res}");

                return 0;
            }

            if (start)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("New account needs a wallet id");

                    return 1;
                }
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                LightClient.Start(config);

                return 0;
            }

            if (infoAcc)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("Account info needs a wallet id");

                    return 1;
                }

                var acc = LightClient.GetAccount(config);
                var info = LightClient.GetAccountInfo(config);

                Console.WriteLine("Account Info: \n");
                Console.WriteLine($"Id:\t\t{info.Id}");
                Console.WriteLine($"Wallet Id:\t{config.WalletId}");
                Console.WriteLine($"Name:\t\t{info.Name}");
                Console.WriteLine($"HD Path:\t{info.HdPath}");
                Console.WriteLine($"Xpub:\t\t{info.Xpub}");
                Console.WriteLine($"Xprv:\t\t{info.Xprv}");

                // Example to get more data, from the acc variable
                Console.WriteLine($"DustMinValue:\t{acc.DustMinValue}");

                return 0;
            }

            if (sync)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("New account needs a wallet id");

                    return 1;
                }
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                LightClient.Sync(config);

                return 0;
            }

            if (resync)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("New account needs a wallet id");

                    return 1;
                }
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                LightClient.ReSync(config);

                return 0;
            }

            if (detectAccount)
            {
                if (string.IsNullOrEmpty(mnemonic))
                {
                    Console.WriteLine("Detect accounts need a mnemonic");

                    return 1;
                }

                var accounts = LightClient.FindAccounts(mnemonic, network);

                if (saveAccounts)
                {
                    Debug.WriteLine("[detectAccount] Save accounts TODO");
                }

                return 0;
            }

            if (ping)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("Ping needs a wallet id, cause... There's no actual reason why but cause");

                    return 1;
                }

                LightClient.Ping(config);

                return 0;
            }

            if (headers)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("Subscribe to headers needs a wallet id");

                    return 1;
                }

                LightClient.HeadersNotifications(config);

                return 0;
            }

            if (coinControl)
            {
                // Check if we want to freeze or unfreeze
                List<string> actions = new();
                if (!string.IsNullOrEmpty(freezeCoin))
                {
                    actions.Add("freeze");
                }

                if (!string.IsNullOrEmpty(unfreezeCoin))
                {
                    actions.Add("unfreeze");
                }

                // We always wanna list the coins at the end
                actions.Add("list");

                foreach (var action in actions)
                {
                    switch (action)
                    {
                        case "freeze":
                            LightClient.FreezeCoin(config, freezeCoin);
                            break;
                        case "unfreeze":
                            LightClient.UnfreezeCoin(config, unfreezeCoin);
                            break;
                        case "list":
                            LightClient.ShowCoins(config);

                            break;
                        default:
                            break;
                    }
                }

                return 0;
            }

            if (dustControl)
            {
                var acc = LightClient.GetAccount(config);

                // -1 is the default 0 will disable it basically
                if (dustAmount >= 0)
                {
                    // Sets dust control amount on the account
                    acc.DustMinValue = dustAmount;
                    acc.Wallet.Storage.Save();
                }

                acc.UpdateDustCoins();
                acc.Wallet.Storage.Save();

                return 0;
            }

            if (send)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("New account needs a wallet id");

                    return 1;
                }
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                Transaction tx = null;
                string error;
                var res = LightClient.Send(
                    config,
                    address, amount, feeSatsPerByte,
                    passphrase: passphrase
                );

                res.Wait();

                tx = res.Result.Transaction;
                error = res.Result.Error;

                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"Error sending transaction {error}");

                    return 1;
                }

                logger.Information("Successfully sent transaction, id: {id}", tx.GetHash().ToString());

                return 0;
            }

            if (bump)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("New account needs a wallet id");

                    return 1;
                }
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                if (string.IsNullOrEmpty(txId))
                {
                    logger.Error("Invalid, txid is required");

                    return 1;
                }

                if (feeSatsPerByte < 1m)
                {
                    logger.Error("Invalid, feeSatsPerByte is required and must be 1.0 or more");

                    return 1;
                }

                var res = LightClient.BumpFee(config, txId, feeSatsPerByte, passphrase);

                res.Wait();

                var tx = res.Result.Transaction;
                var error = res.Result.Error;

                if (string.IsNullOrEmpty(error))
                    logger.Information("Bumped transaction with id: {txId}, new tx id: {newTxId}, new fee: {fee}", txId, tx.GetHash().ToString(), feeSatsPerByte);
                else
                    logger.Error("Failed to bump transaction with id: {txId}, error: {error}", txId, error);

                return 0;
            }

            if (balance)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("New account needs a wallet id");

                    return 1;
                }
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                if (accountIndex != -1)
                {
                    var balance = LightClient.AccountBalance(config, null, accountIndex);

                    Console.WriteLine($"{accountIndex} = {balance}");

                    return 0;
                }

                var accountsWithBalance = LightClient.AllAccountsBalances(config);

                foreach (var entry in accountsWithBalance)
                    Console.WriteLine($"{entry.Key.Index} = {entry.Value}");

                return 0;
            }

            if (summary)
            {
                if (string.IsNullOrEmpty(config.WalletId))
                {
                    Console.WriteLine("New account needs a wallet id");

                    return 1;
                }
                else
                    logger.Information("Using wallet id: {walletId}", config.WalletId);

                if (accountIndex != -1)
                {
                    LightClient.AccountSummary(config, null, accountIndex);

                    return 0;
                }

                LightClient.AllAccountsSummaries(config);

                return 0;
            }

            // Debug commands
            if (testStuff)
            {
                LightClient.TestStuff(config);

                return 0;
            }

            // End... invalid options
            InvalidArguments();

            return 1;
        }

        /// <summary>
        /// Display cli command help
        /// </summary>
        /// <param name="options">An <see cref="OptionSet"/> with the options that ran</param>
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

        /// <summary>
        /// Displays invalid argument message
        /// </summary>
        /// <param name="msg">A <see cref="string"/> with a custom message</param>
        static void InvalidArguments(string msg = "Invalid argument options.")
        {
            Console.WriteLine($"{msg}\n");

            ShowHelp(options);
        }
    }
}
