using System;
using System.IO;
using System.Threading.Tasks;

using Serilog;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

using Liviano.Models;
using Liviano.Utilities;
using Liviano.Managers;
using Liviano.Behaviors;

namespace Liviano.CLI
{
    public class SPVClient
    {
        private static NodesGroup _Group;

        private static object _Lock = new object();

        private static NodeConnectionParameters _ConParams;

        private static ILogger _Logger;

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
            if (_ConParams != null)
            {
                return _ConParams.TemplateBehaviors.Find<AddressManagerBehavior>().AddressManager;

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

        private static ConcurrentChain GetChain()
        {
            lock (_Lock)
            {
                if (_ConParams != null)
                {
                    return _ConParams.TemplateBehaviors.Find<ChainBehavior>().Chain;
                }
                var chain = new ConcurrentChain(Network.TestNet);
                using (var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                {
                    chain.Load(fs);

                }
                return chain;
            }
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

        private static async Task SaveAsync()
        {
            await Task.Factory.StartNew(() =>
            {
                lock (_Lock)
                {
                    GetAddressManager().SavePeerFile(AddrmanFile(), Network.TestNet);
                    using (var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                    {
                        GetChain().WriteTo(fs);
                    }
                }
            });
        }

        public static void Start(string walletFileId, string network)
        {
            Network bitcoinNetwork = HdOperations.GetNetwork(network);

            Start(walletFileId, bitcoinNetwork);
        }

        public static void Start(string walletFileId, Network network)
        {
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

            _ConParams = parameters;

            PeriodicSave();

            // Events examples
            // With static methods
            walletSyncManager.OnWalletPositionUpdate += WalletSyncManager_OnWalletPositionUpdate;

            // With lambdas
            walletSyncManager.OnWalletSyncedToTipOfChain += (sender, chainedBlock) => { _Logger.Information("Finished Syncing to up to tip: {tip}", chainedBlock.Height); };
            walletManager.OnNewTransaction += (sender, transactionData) => { _Logger.Information("New tx: {txId}", transactionData.Id); };
            walletManager.OnUpdateTransaction += (sender, transactionData) => { _Logger.Information("Updated tx: {txId}", transactionData.Id); };
            walletManager.OnNewSpendingTransaction += (sender, spendingTransaction) => { _Logger.Information("New spending tx: {txId}", spendingTransaction.Id); };
            walletManager.OnUpdateSpendingTransaction += (sender, spendingTransaction) => { _Logger.Information("Update spending tx: {txId}", spendingTransaction.Id); };

            WaitUntilCtrlC();
        }

        private static void WaitUntilCtrlC()
        {
            bool quit = false;

            _Logger.Information("Press CTRL+C to stop SPV client...");
            
            while(!quit)
            {
                var keyInfo = Console.ReadKey();

                quit = keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers == ConsoleModifiers.Control;
            }
        }
    }
}
