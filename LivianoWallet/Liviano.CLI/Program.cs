using System;
using System.IO;
using System.Collections.Generic;

using Mono.Options;
using NBitcoin;
using Serilog;

using Liviano.Bips;
using Liviano.Exceptions;
using System.Text;

namespace Liviano.CLI
{
    class Program
    {
        ILogger _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        static void Main(string[] args)
        {
            // Defaults
            var network = Network.Main;
            var mnemonic = "";
            var mnemonicLang = "english";
            var passphrase = "";
            var mnemonicWordCount = 12;
            var inputText = Console.IsInputRedirected ? Console.ReadLine().Trim() : "";
            var hasInputText = !string.IsNullOrEmpty(inputText);
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

            // End... invalid options
            Console.WriteLine("Invalid argument optoins.\n");
            LightClient.ShowHelp();

            //.WithParsed<NewWalletOptions>(o =>
            //{
            //    string mnemonic = null;
            //    string name = Guid.NewGuid().ToString();
            //    string network = "main";

            //    if (o.Mnemonic != null)
            //    {
            //        mnemonic = o.Mnemonic;
            //    }
            //    else
            //    {
            //        mnemonic = Console.ReadLine();
            //    }

            //    if (o.Name != null)
            //    {
            //        name = o.Name;
            //    }

            //    _Logger.Information("New wallet with name {name} to be created", name);

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //    }

            //    if (string.IsNullOrEmpty(mnemonic))
            //    {
            //        _Logger.Error("Empty mnemonic");

            //        _Logger.Information("Generating new mnemonic");

            //        mnemonic = new Mnemonic(Hd.WordlistFromString(), Hd.WordCountFromInt()).ToString();
            //    }

            //    // If the configuration exists, we just add a new wallet
            //    Config config;
            //    if (Config.Exists())
            //    {
            //        config = Config.Load();

            //        if (config.HasWallet(name))
            //        {
            //            _Logger.Error("Wallet already exists: {name}", name);

            //            throw new WalletException($"Wallet already exists {name}");
            //        }

            //        config.WalletId = name;
            //        config.Network = network;

            //        config.Add(name);

            //        config.SaveChanges();
            //    }
            //    else
            //    {
            //        config = new Config(name, network);
            //    }

            //    config.SaveChanges();

            //    LightClient.CreateWallet(config, o.Password, mnemonic);

            //    Console.WriteLine(name);
            //})
            //.WithParsed<WalletBalanceOptions>(o =>
            //{
            //    string walletId = null;
            //    Config config = null;

            //    if (o.WalletId != null)
            //    {
            //        walletId = o.WalletId;
            //    }

            //    if (Config.Exists())
            //    {
            //        config = Config.Load();

            //        if (walletId != null && !config.HasWallet(walletId))
            //        {
            //            _Logger.Error("Please create a new wallet for {walletId}", walletId);

            //            throw new WalletException($"Please create a new wallet for {walletId}");
            //        }

            //        walletId = config.WalletId;
            //    }
            //    else
            //    {
            //        _Logger.Error("Client configuration not found, use the command new-wallet to initalize your wallet with a mnemonic");

            //        throw new WalletException("Please create a new wallet with the command new-wallet");
            //    }

            //    if (o.Testnet)
            //    {
            //        config.Network = "testnet";
            //    }

            //    config.SaveChanges();

            //    bool shownBalance = false;

            //    try
            //    {
            //        if (o.Name != null)
            //        {
            //            Money confirmedAmount = LightClient.AccountBalance(config, o.Password, o.Name);

            //            Console.WriteLine("Name, Amount");
            //            Console.WriteLine("============");

            //            Console.WriteLine($"{o.Name}, {confirmedAmount}");

            //            shownBalance = true;
            //        }

            //        if (o.Index != null)
            //        {
            //            Money confirmedAmount = LightClient.AccountBalance(config, o.Password, accountIndex: o.Index);

            //            Console.WriteLine("Name, Amount");
            //            Console.WriteLine("============");

            //            Console.WriteLine($"{o.Name}, {confirmedAmount}");

            //            shownBalance = true;
            //        }
            //    }
            //    catch (WalletException e)
            //    {
            //        _Logger.Error(e.ToString());

            //        Console.WriteLine($"Account ({o.Name ?? o.Index}) not found.");
            //    }

            //    if (!shownBalance)
            //    {
            //        //var balances = LightClient.AllAccountsBalance(config, o.Password);

            //        //if (balances.Count() > 0)
            //        //{
            //        //    Console.WriteLine("Name, HdPath, Confirmed Amount, Unconfirmed Amount");
            //        //    Console.WriteLine("==================================================");

            //        //    foreach (var balance in balances)
            //        //    {
            //        //        Console.WriteLine($"{balance.Name}, {balance.HdPath}, {balance.ConfirmedAmount}, {balance.UnConfirmedAmount}");
            //        //    }
            //        //}
            //        //else
            //        //{
            //        //    Console.WriteLine("No accounts with balances found");
            //        //}
            //        throw new NotImplementedException("Upps we haven't done this!");
            //    }
            //})
            //.WithParsed<NewAddressOptions>(o =>
            //{
            //    string walletId = null;
            //    Config config = null;

            //    if (o.WalletId != null)
            //    {
            //        walletId = o.WalletId;
            //    }

            //    if (Config.Exists())
            //    {
            //        config = Config.Load();

            //        if (walletId != null && !config.HasWallet(walletId))
            //        {
            //            _Logger.Error("Please create a new wallet for {walletId}", walletId);

            //            throw new WalletException($"Please create a new wallet for {walletId}");
            //        }

            //        walletId = config.WalletId;
            //    }
            //    else
            //    {
            //        _Logger.Error("Client configuration not found, use the command new-wallet to initalize your wallet with a mnemonic");

            //        throw new WalletException("Please create a new wallet with the command new-wallet");
            //    }

            //    if (o.Testnet)
            //    {
            //        config.Network = "testnet";
            //    }

            //    config.SaveChanges();

            //    BitcoinAddress address = null;

            //    try
            //    {
            //        if (o.Name == null && o.Index == null)
            //        {
            //            address = LightClient.GetAddress(config, o.Password);
            //        }
            //        else if (o.Name != null)
            //        {
            //            address = LightClient.GetAddress(config, o.Password, accountName: o.Name);
            //        }
            //        else if (o.Index != null)
            //        {
            //            address = LightClient.GetAddress(config, o.Password, accountIndex: o.Index);
            //        }
            //    }
            //    catch (InvalidOperationException e)
            //    {
            //        _Logger.Error(e.ToString());

            //        Console.WriteLine($"Unable to find account ({o.Name ?? o.Index})");

            //        return;
            //    }

            //    Console.WriteLine($"{address.ToString()}");
            //})
            //.WithParsed<SendOptions>(async o =>
            //{
            //    string walletId = null;
            //    Config config = null;

            //    if (o.WalletId != null)
            //    {
            //        walletId = o.WalletId;
            //    }

            //    if (Config.Exists())
            //    {
            //        config = Config.Load();

            //        if (walletId != null && !config.HasWallet(walletId))
            //        {
            //            _Logger.Error("Please create a new wallet for {walletId}", walletId);

            //            throw new WalletException($"Please create a new wallet for {walletId}");
            //        }

            //        walletId = config.WalletId;
            //    }
            //    else
            //    {
            //        _Logger.Error("Client configuration not found, use the command new-wallet to initalize your wallet with a mnemonic");

            //        throw new WalletException("Please create a new wallet with the command new-wallet");
            //    }

            //    if (o.Testnet)
            //    {
            //        config.Network = "testnet";
            //    }

            //    config.SaveChanges();

            //    bool wasCreated = false;
            //    bool wasSent = false;
            //    Transaction tx = null;
            //    string error = null;

            //    if (o.Name == null && o.Index == null)
            //    {
            //        (wasCreated, wasSent, tx, error) = await LightClient.Send(config, o.Password, o.To, o.Amount, o.SatsPerByte);
            //    }
            //    else if (o.Name != null)
            //    {
            //        (wasCreated, wasSent, tx, error) = await LightClient.Send(config, o.Password, o.To, o.Amount, o.SatsPerByte, accountName: o.Name);
            //    }
            //    else if (o.Index != null)
            //    {
            //        (wasCreated, wasSent, tx, error) = await LightClient.Send(config, o.Password, o.To, o.Amount, o.SatsPerByte, accountIndex: o.Index);
            //    }

            //    Console.WriteLine($"TxId: {tx.GetHash()}");
            //    Console.WriteLine("=====================" + new string('=', tx.GetHash().ToString().Length));
            //    Console.WriteLine($"Size: {tx.GetVirtualSize()}");
            //    Console.WriteLine($"Created: {wasCreated}");
            //    Console.WriteLine($"Sent: {wasSent}");
            //    Console.WriteLine("Inputs");
            //    Console.WriteLine("------");

            //    foreach (var input in tx.Inputs)
            //    {
            //        Console.WriteLine($"{input.PrevOut.Hash} ({input.PrevOut.N})");
            //    }

            //    Console.WriteLine($"Fees: {new Money((long)tx.GetVirtualSize() * o.SatsPerByte).ToDecimal(MoneyUnit.BTC)}");

            //    Console.WriteLine($"Hex: {tx.ToHex()}");
            //})
            //.WithParsed<StartOptions>(o =>
            //{
            //    string network = "main";
            //    string walletId = null;
            //    Config config = null;

            //    if (o.WalletId != null)
            //    {
            //        walletId = o.WalletId;
            //    }

            //    if (Config.Exists())
            //    {
            //        config = Config.Load();

            //        if (walletId != null && !config.HasWallet(walletId))
            //        {
            //            _Logger.Error("Please create a new wallet for {walletId}", walletId);

            //            throw new WalletException($"Please create a new wallet for {walletId}");
            //        }

            //        walletId = config.WalletId;
            //    }
            //    else
            //    {
            //        _Logger.Error("Client configuration not found, use the command new-wallet to initalize your wallet with a mnemonic");

            //        throw new WalletException("Please create a new wallet with the command new-wallet");
            //    }

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //        config.Network = network;
            //    }

            //    if (o.NodesToConnect != 0)
            //    {
            //        config.NodesToConnect = o.NodesToConnect;
            //    }
            //    else if (config.NodesToConnect == 0)
            //    {
            //        config.NodesToConnect = 4; // safe default.
            //    }

            //    config.SaveChanges();

            //    LightClient.Start(config, o.Resync);
            //})
            //.WithParsed<ElectrumTestOptions>(o =>
            //{
            //    Config config;
            //    string network;

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //    }
            //    else
            //    {
            //        network = "main";
            //    }

            //    if (Config.Exists())
            //    {
            //        config = Config.Load();
            //    }
            //    else
            //    {
            //        config = new Config("electrum-test", network);
            //    }

            //    config.Network = network;

            //    config.SaveChanges();

            //    if (network == "main")
            //    {

            //        LightClient.TestElectrumConnection("1HgFikZZi9C4t7B1gRohtiEon2FtmDvwnu", "fbe0b33f38059e88840f8d222597e1b6edd5280663094f4b3300f15551dc6b74", Network.Main);
            //    }
            //    else
            //    {
            //        LightClient.TestElectrumConnection("2NDkEKKqP5rqhMYBq4JSXn8LppFFdSg6gtn", "eae1eeba5a5fb87629e362e346688443d106bb4e835798ec0f311da75b9cc80f", Network.TestNet);
            //    }
            //}).WithParsed<ElectrumTest2Options>(o =>
            //{
            //    Config config;
            //    string network;

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //    }
            //    else
            //    {
            //        network = "mainnet";
            //    }

            //    if (Config.Exists())
            //    {
            //        config = Config.Load();
            //    }
            //    else
            //    {
            //        config = new Config("electrum-test2", network);
            //    }

            //    config.Network = network;
            //    config.SaveChanges();

            //    if (network == "mainnet")
            //    {
            //        LightClient.TestElectrumConnection2(Network.Main);
            //    }
            //    else
            //    {
            //        LightClient.TestElectrumConnection2(Network.TestNet);
            //    }
            //}).WithParsed<ElectrumTest3Options>(o =>
            //{
            //    Config config;
            //    string network;

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //    }
            //    else
            //    {
            //        network = "mainnet";
            //    }

            //    if (Config.Exists())
            //    {
            //        config = Config.Load();
            //    }
            //    else
            //    {
            //        config = new Config("electrum-test3", network);
            //    }

            //    config.Network = network;
            //    config.SaveChanges();

            //    if (network == "mainnet")
            //    {
            //        LightClient.TestElectrumConnection3(Network.Main);
            //    }
            //    else
            //    {
            //        LightClient.TestElectrumConnection3(Network.TestNet);
            //    }
            //});
        }
    }
}
