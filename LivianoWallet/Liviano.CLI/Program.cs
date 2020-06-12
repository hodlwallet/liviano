using System;
using System.Collections.Generic;
using NBitcoin;
using Serilog;

using Mono.Options;

using Liviano.Exceptions;
using Liviano.Bips;
using System.Text;

namespace Liviano.CLI
{
    class Program
    {
        private static ILogger _Logger;

        static void Main(string[] args)
        {
            _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            // Defaults
            var network = Network.Main;
            var showHelp = false;
            var testnet = false;
            var mainnet = true;
            var electrumTest3 = false;

            // Define options
            var options = new OptionSet
            {
                {"m|mainnet", "Run on mainnet", m => mainnet = !(m is null)},
                {"t|testnet", "Run on testnet", t => testnet = !(t is null)},
                {"h|help", "Liviano help", h => showHelp = !(h is null)},
                // Debugging commands
                {"et3|electrum-test-3", "Electrum test 3", et3 => electrumTest3 = !(et3 is null)}
            };

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

            // Set variaables every command use
            if (testnet)
            {
                mainnet = false;

                network = Network.TestNet;
            }

            if (mainnet)
            {
                network = Network.Main;
            }

            // LightClient commands, set everything before here
            if (electrumTest3)
            {
                LightClient.TestElectrumConnection3(network);

                return;
            }

            // End... invalid options
            Console.WriteLine("Invalid options");
            Console.WriteLine("Show Help");

            //LightClient.TestElectrumConnection3(Network.TestNet);

            //_ = Parser.Default.ParseArguments<MnemonicOptions, ExtendedKeyOptions, ExtendedPubKeyOptions, DeriveAddressOptions, AddressToScriptPubKeyOptions, NewWalletOptions, WalletBalanceOptions, NewAddressOptions, SendOptions, StartOptions, ElectrumTestOptions, ElectrumTest2Options, ElectrumTest3Options>(args)
            //.WithParsed<MnemonicOptions>(o =>
            //{
            //    string wordlist = "english";
            //    int wordCount = 24;

            //    if (o.WordCount != 0)
            //    {
            //        wordCount = o.WordCount;
            //    }

            //    if (o.Wordlist != null)
            //    {
            //        wordlist = o.Wordlist;
            //    }

            //    Console.WriteLine(Hd.NewMnemonic(wordlist, wordCount));
            //})
            //.WithParsed<ExtendedKeyOptions>(o =>
            //{
            //    string mnemonic = null;
            //    string passphrase = null;
            //    string network = "main";

            //    if (o.Mnemonic != null)
            //    {
            //        mnemonic = o.Mnemonic;
            //    }
            //    else
            //    {
            //        mnemonic = Console.ReadLine();
            //    }

            //    if (o.Passphrase != null)
            //    {
            //        passphrase = o.Passphrase;
            //    }

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //    }
            //    else if (o.Regtest)
            //    {
            //        network = "regtest";
            //    }

            //    var extKey = Hd.GetExtendedKey(mnemonic, passphrase);
            //    var wif = Hd.GetWif(extKey, network);

            //    Console.WriteLine(wif);
            //})
            //.WithParsed<ExtendedPubKeyOptions>(o =>
            //{
            //    string wif;
            //    string hdPath = "m/84'/0'/0'/0/0"; // Default BIP84 / Bitcoin / 1st account / receive / 1st pubkey
            //    string network = "main";

            //    if (o.Wif != null)
            //    {
            //        wif = o.Wif;
            //    }
            //    else
            //    {
            //        wif = Console.ReadLine();
            //    }

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //    }
            //    else if (o.Regtest)
            //    {
            //        network = "regtest";
            //    }

            //    if (o.HdPath != null)
            //    {
            //        hdPath = o.HdPath;
            //    }

            //    var extPubKey = Hd.GetExtendedPublicKey(wif, hdPath, network);
            //    var extPubKeyWif = Hd.GetWif(extPubKey, network);

            //    Console.WriteLine(extPubKeyWif);
            //})
            //.WithParsed<DeriveAddressOptions>(o =>
            //{
            //    string wif;
            //    int index = 1;
            //    bool isChange = false;
            //    string network = "main";
            //    string type = "p2wpkh";

            //    if (o.Wif != null)
            //    {
            //        wif = o.Wif;
            //    }
            //    else
            //    {
            //        wif = Console.ReadLine();
            //    }

            //    if (o.Index != null)
            //    {
            //        index = (int)o.Index;
            //    }

            //    if (o.IsChange)
            //    {
            //        isChange = o.IsChange;
            //    }

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //    }
            //    else if (o.Regtest)
            //    {
            //        network = "regtest";
            //    }

            //    if (o.Type != null)
            //    {
            //        type = o.Type;
            //    }

            //    Console.WriteLine(Hd.GetAddress(wif, index, isChange, network, type));
            //})
            //.WithParsed<AddressToScriptPubKeyOptions>(o =>
            //{
            //    string network = "main";
            //    string address = null;

            //    if (o.Testnet)
            //    {
            //        network = "testnet";
            //    }
            //    else if (o.Regtest)
            //    {
            //        network = "regtest";
            //    }

            //    if (o.Address != null)
            //    {
            //        address = o.Address;
            //    }
            //    else
            //    {
            //        address = Console.ReadLine();
            //    }

            //    Console.WriteLine(Hd.GetScriptPubKey(address, network));
            //})
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
