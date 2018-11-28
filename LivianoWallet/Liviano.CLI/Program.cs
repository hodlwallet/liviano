using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Liviano;
using Liviano.Behaviors;
using Liviano.Managers;
using Liviano.Models;
using Liviano.Utilities;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.SPV;
using Serilog;
using Easy.MessageHub;

namespace Liviano.CLI
{
    class Program
    {
        private static NodesGroup _Group;

        private static object locker = new object();

        private static NodeConnectionParameters conparams;

        private static ILogger _Logger;

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
            var network = Network.TestNet;
            var chain = GetChain();
            var asyncLoopFactory = new AsyncLoopFactory();
            var dateTimeProvider = new DateTimeProvider();
            var scriptAddressReader = new ScriptAddressReader();
            var storageProvider = new FileSystemStorageProvider(walletFileId);

            _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            _Logger.Information("Starting wallet for file: {waleltFileId} on {network}", walletFileId, network.Name);

            WalletManager walletManager = new WalletManager(_Logger, network, chain, asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider);
            WalletSyncManager walletSyncManager = new WalletSyncManager(walletManager, chain, _Logger);

            var m = new Mnemonic("october wish legal icon nest forget jeans elite cream account drum into");
            walletManager.CreateWallet("1111", "test", m);

            var parameters = new NodeConnectionParameters();

            parameters.TemplateBehaviors.Add(new AddressManagerBehavior(GetAddressManager())); //So we find nodes faster
            parameters.TemplateBehaviors.Add(new ChainBehavior(chain)); //So we don't have to load the chain each time we start
            parameters.TemplateBehaviors.Add(new WalletSyncManagerBehavior(walletSyncManager, _Logger));

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
            walletManager.CreationTime = new DateTimeOffset(new DateTime(2018, 11, 10));
            walletSyncManager.Scan(ScanLocation, walletManager.CreationTime);

            conparams = parameters;

            PeriodicSave();

            // Events examples
            // With static methods
            walletSyncManager.OnWalletPositionUpdate += WalletSyncManager_OnWalletPositionUpdate;

            // With lambdas
            walletSyncManager.OnWalletSyncedToTipOfChain += (sender, chainedBlock) => { _Logger.Information("Change tip to {tip}", chainedBlock.Height); };
            walletManager.OnNewTransaction += (sender, transactionData) => { _Logger.Information("New tx: {txId}", transactionData.Id); };
            walletManager.OnUpdateTransaction += (sender, transactionData) => { _Logger.Information("Updated tx: {txId}", transactionData.Id); };
            walletManager.OnNewSpendingTransaction += (sender, spendingTransaction) => { _Logger.Information("New spending tx: {txId}", spendingTransaction.Id); };
            walletManager.OnUpdateSpendingTransaction += (sender, spendingTransaction) => { _Logger.Information("Update spending tx: {txId}", spendingTransaction.Id); };

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        private static void WalletSyncManager_OnWalletPositionUpdate(object sender, WalletPositionUpdatedEventArgs walletPositionUpdate)
        {
            _Logger.Information("Position updated to: {height}", walletPositionUpdate.NewPosition.Height);
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
