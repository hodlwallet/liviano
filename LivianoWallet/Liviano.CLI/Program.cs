using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommandLine;

using Liviano;
using Liviano.Utilities;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.SPV;
using Serilog;

namespace Liviano.CLI
{
    class Program
    {
        private static NodesGroup _Group;
        private static object locker = new object();
        private static NodeConnectionParameters conparams;
        static void Main(string[] args)
        {
            //Parser.Default.ParseArguments<NewMnemonicOptions, GetExtendedKeyOptions, GetExtendedPubKeyOptions, DeriveAddressOptions, AddressToScriptPubKeyOptions>(args)
            //.WithParsed<NewMnemonicOptions>(o => {
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

            //    Console.WriteLine(WalletManager.NewMnemonic(wordlist, wordCount).ToString());
            //})
            //.WithParsed<GetExtendedKeyOptions>(o => {
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

            //    var extKey = HdOperations.GetExtendedKey(mnemonic, passphrase);
            //    var wif = HdOperations.GetWif(extKey, network);

            //    Console.WriteLine(wif.ToString());
            //})
            //.WithParsed<GetExtendedPubKeyOptions>(o => {
            //    string wif;
            //    string hdPath = "m/44'/0'/0'/0/0"; // Default BIP44 / Bitcoin / 1st account / receive / 1st pubkey
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

            //    var extPubKey = HdOperations.GetExtendedPublicKey(wif, hdPath, network);
            //    var extPubKeyWif = HdOperations.GetWif(extPubKey, network);

            //    Console.WriteLine(extPubKeyWif.ToString());
            //})
            //.WithParsed<DeriveAddressOptions>(o => {
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
            //        index = (int) o.Index;
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

            //    Console.WriteLine(HdOperations.GetAddress(wif, index, isChange, network, type).ToString());
            //})
            //.WithParsed<AddressToScriptPubKeyOptions>(o => {
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

            //    Console.WriteLine(HdOperations.GetScriptPubKey(address, network).ToString());
            //});
            var walletFileId = "c5cfc267-b75a-41bc-bdb5-8b67299d04f4"; //"c5cfc267-b75a-41bc-bdb5-8b67299d04f4";
            var logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
            var network = Network.TestNet;
            var chain = GetChain();
            var asyncLoopFactory = new AsyncLoopFactory();
            var dateTimeProvider = new DateTimeProvider();
            var scriptAddressReader = new ScriptAddressReader();
            var storageProvider = new FileSystemStorageProvider(id: walletFileId);


            WalletManager walletManager = new WalletManager(logger,network,chain,asyncLoopFactory,dateTimeProvider,scriptAddressReader,storageProvider);
            WalletSyncManager walletSyncManager = new WalletSyncManager(walletManager, chain, logger);


            var m = new Mnemonic("october wish legal icon nest forget jeans elite cream account drum into");
            walletManager.CreateWallet("1111","test",m);
           // walletManager.Wallet.AddNewAccount("1111", CoinType.Bitcoin, DateTimeOffset.Now);
            //walletManager.SaveWallet(walletManager.Wallet);

            var parameters = new NodeConnectionParameters();
            //parameters.TemplateBehaviors.Add(new TrackerBehavior(GetTracker())); //Tracker knows which scriptPubKey and outpoints to track, it monitors all your wallets at the same
            parameters.TemplateBehaviors.Add(new AddressManagerBehavior(GetAddressManager())); //So we find nodes faster
            parameters.TemplateBehaviors.Add(new ChainBehavior(chain)); //So we don't have to load the chain each time we start
            parameters.TemplateBehaviors.Add(new WalletSyncManagerBehavior(walletSyncManager, logger: logger));
            _Group = new NodesGroup(Network.TestNet, parameters, new NodeRequirement()
            {
                RequiredServices = NodeServices.Network //Needed for SPV
            });
            _Group.MaximumNodeConnection = 4;
            _Group.Connect();

            var broadcastManager = new BroadcastManager(_Group);
           



            walletManager.Start();
            var ScanLocation = new BlockLocator();
            ScanLocation.Blocks.Add(Network.TestNet.GenesisHash);
            walletSyncManager.Scan(ScanLocation, new DateTimeOffset(new DateTime(2018, 11, 1)));


            conparams = parameters;
            //WalletCreation creation = new WalletCreation()
            //{
            //    Name = "Test",
            //    UseP2SH = true,
            //    SignatureRequired = 0,
            //    RootKeys = new ExtPubKey[] { new Mnemonic("october wish legal icon nest forget jeans elite cream account drum into").DeriveExtKey().Derive(new KeyPath($"m/44'/0'/1'")).GetWif(Network.TestNet).Neuter() },
            //    Network = Network.TestNet
            //};

            //var wallet = new NBitcoin.SPV.Wallet(creation);


            //wallet.Configure(_Group);
            //wallet.Connect();
            //Console.WriteLine(wallet.GetNextScriptPubKey().GetDestinationAddress(Network.TestNet));

            //wallet.Created = new DateTimeOffset(new DateTime(2018, 11, 12));
            //wallet.Rescan(Network.TestNet.GenesisHash);
            PeriodicSave();

            Console.ReadLine();

        }

        private static async void PeriodicSave()
        {
            while (1 == 1)
            {
                await Task.Delay(50_000);
             
                    await SaveAsync();

            }
        }

        private static AddressManager GetAddressManager()
        {
            if (conparams != null)
            {
                return conparams.TemplateBehaviors.Find<AddressManagerBehavior>().AddressManager;

            }

            if (File.Exists(AddrmanFile()))
            {
                return AddressManager.LoadPeerFile(AddrmanFile(), Network.TestNet);
            }
            else
            {
                return new AddressManager();
            }
        }

        private static Tracker GetTracker()
        {
            lock (locker)
            {
                if (conparams != null)
                {
                    return conparams.TemplateBehaviors.Find<TrackerBehavior>().Tracker;

                }
                using (var fs = File.Open(TrackerFile(), FileMode.OpenOrCreate))
                {
                    return Tracker.Load(fs);

                }
                return new Tracker();
            }

        }

        private static ConcurrentChain GetChain()
        {
            lock (locker)
            {
                if (conparams != null)
                {
                    return conparams.TemplateBehaviors.Find<ChainBehavior>().Chain;
                }
                var chain = new ConcurrentChain(Network.TestNet);
                using (var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                {
                    chain.Load(fs);

                }
                return chain;
            }

            return new ConcurrentChain(Network.TestNet);

        }

        private static string AddrmanFile()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "data", "addrman.dat");
        }

        private static string TrackerFile()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "data", "tracker.dat");
        }

        private static string ChainFile()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "data", "chain.dat");
        }

        private static string WalletFile()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "data", "wallet.dat");
        }

        private static async Task SaveAsync()
        {
            await Task.Factory.StartNew(() =>
            {
                lock (locker)
                {
                    GetAddressManager().SavePeerFile(AddrmanFile(), Network.TestNet);
                    using (var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                    {
                        GetChain().WriteTo(fs);
                    }
                    //using (var fs = File.Open(TrackerFile(), FileMode.OpenOrCreate))
                    //{
                    //    GetTracker().Save(fs);
                    //}
                }
            });
        }
    }
}
