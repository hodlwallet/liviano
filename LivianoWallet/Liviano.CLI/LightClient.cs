using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Serilog;

using Liviano.Behaviors;
using Liviano.Exceptions;
using Liviano.Managers;
using Liviano.Models;
using Liviano.Utilities;

namespace Liviano.CLI
{
    public class LightClient
    {
        private static NodesGroup _Group;

        private static object _Lock = new object();

        private static NodeConnectionParameters _ConParams;

        private static ILogger _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        private static Network _Network;

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
                return AddressManager.LoadPeerFile(AddrmanFile(), _Network);
            }
            else
            {
                return new AddressManager();
            }
        }

        private static ConcurrentChain GetChain()
        {
            lock(_Lock)
            {
                if (_ConParams != null)
                {
                    return _ConParams.TemplateBehaviors.Find<ChainBehavior>().Chain;
                }
                var chain = new ConcurrentChain(_Network);
                using(var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                {
                    chain.Load(fs);

                }

                return chain;
            }
        }

        private static string GetConfigFile(string fileName)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "data", fileName);
        }

        private static string AddrmanFile()
        {
            return GetConfigFile("addrman.dat");
        }

        private static string ChainFile()
        {
            return GetConfigFile("chain.dat");
        }

        private static async Task SaveAsync()
        {
            await Task.Factory.StartNew(() =>
            {
                lock(_Lock)
                {
                    GetAddressManager().SavePeerFile(AddrmanFile(), _Network);
                    using(var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                    {
                        GetChain().WriteTo(fs);
                    }
                }
            });
        }

        public static (string Name, string HdPath, Money ConfirmedAmount, Money UnConfirmedAmount) AccountBalance(Config config, string password, string accountName = null, string accountIndex = null)
        {
            if (accountIndex == null) accountIndex = "-1";

            try
            {
                return AllAccountsBalance(config, password).First(b => b.Name == accountName || b.HdPath.EndsWith($"{accountIndex}'"));
            }
            catch (InvalidOperationException e)
            {
                _Logger.Error(e.ToString());

                throw new WalletException($"Could not find account index ({accountIndex}) or name ({accountName})");
            }
        }

        public static IEnumerable<(string Name, string HdPath, Money ConfirmedAmount, Money UnConfirmedAmount)> AllAccountsBalance(Config config, string password)
        {
            List<(string, string, Money, Money)> balances = new List<(string, string, Money, Money)>();

            _Network = HdOperations.GetNetwork(config.Network);

            var chain = new ConcurrentChain();
            var asyncLoopFactory = new AsyncLoopFactory();
            var dateTimeProvider = new DateTimeProvider();
            var scriptAddressReader = new ScriptAddressReader();
            var storageProvider = new FileSystemStorageProvider(config.WalletId);

            WalletManager walletManager = new WalletManager(_Logger, _Network, chain, asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider);

            if (!storageProvider.WalletExists())
            {
                _Logger.Error("Error creating wallet from {walletId}", config.WalletId);

                throw new WalletException($"Error creating wallet from wallet id");
            }

            walletManager.LoadWallet(password);

            foreach (var account in walletManager.GetAllAccountsByCoinType(CoinType.Bitcoin))
            {
                var spendableAmounts = account.GetSpendableAmount();

                balances.Add((account.Name ?? $"#{account.Index}", account.HdPath, spendableAmounts.ConfirmedAmount, spendableAmounts.UnConfirmedAmount));
            }

            return balances;
        }

        public static HdAddress GetAddress(Config config, string password, string accountIndex = null, string accountName = null)
        {
            _Network = HdOperations.GetNetwork(config.Network);

            var chain = new ConcurrentChain();
            var asyncLoopFactory = new AsyncLoopFactory();
            var dateTimeProvider = new DateTimeProvider();
            var scriptAddressReader = new ScriptAddressReader();
            var storageProvider = new FileSystemStorageProvider(config.WalletId);

            WalletManager walletManager = new WalletManager(_Logger, _Network, chain, asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider);

            walletManager.LoadWallet(password);

            HdAccount account = null;

            if (accountIndex == null && accountName == null)
            {
                account = walletManager.GetAllAccountsByCoinType(CoinType.Bitcoin).First(o => o.Index == 0);
            }
            else if (accountIndex != null)
            {
                account = walletManager.GetAllAccountsByCoinType(CoinType.Bitcoin).First(o => o.Index == int.Parse(accountIndex));
            }
            else if (accountName != null)
            {
                account = walletManager.GetAllAccountsByCoinType(CoinType.Bitcoin).First(o => o.Name == accountName);
            }

            return account.GetFirstUnusedReceivingAddress();
        }

        public static void CreateWallet(Config config, string password, string mnemonic)
        {
            _Network = HdOperations.GetNetwork(config.Network);

            var chain = new ConcurrentChain();
            var asyncLoopFactory = new AsyncLoopFactory();
            var dateTimeProvider = new DateTimeProvider();
            var scriptAddressReader = new ScriptAddressReader();
            var storageProvider = new FileSystemStorageProvider(config.WalletId);

            _Logger.Information("Creating wallet for file: {walletFileId} on {network}", config.WalletId, _Network.Name);

            WalletManager walletManager = new WalletManager(_Logger, _Network, chain, asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider);

            walletManager.CreateWallet(password, config.WalletId, WalletManager.MnemonicFromString(mnemonic));
        }

        public static void Start(Config config, string password)
        {
            _Network = HdOperations.GetNetwork(config.Network);

            var chain = GetChain();
            var asyncLoopFactory = new AsyncLoopFactory();
            var dateTimeProvider = new DateTimeProvider();
            var scriptAddressReader = new ScriptAddressReader();
            var storageProvider = new FileSystemStorageProvider(config.WalletId);

            _Logger.Information("Starting wallet for file: {walletFileId} on {network}", config.WalletId, _Network.Name);

            WalletManager walletManager = new WalletManager(_Logger, _Network, chain, asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider);
            WalletSyncManager walletSyncManager = new WalletSyncManager(_Logger, walletManager, chain);

            if (!storageProvider.WalletExists())
            {
                _Logger.Error("Error creating wallet from {walletId}", config.WalletId);

                throw new WalletException($"Error creating wallet from wallet id");
            }

            walletManager.LoadWallet(password);

            var parameters = new NodeConnectionParameters();

            parameters.TemplateBehaviors.Add(new AddressManagerBehavior(GetAddressManager())); //So we find nodes faster
            parameters.TemplateBehaviors.Add(new ChainBehavior(chain)); //So we don't have to load the chain each time we start
            parameters.TemplateBehaviors.Add(new WalletSyncManagerBehavior(_Logger, walletSyncManager,Enums.ScriptTypes.Segwit));

            _Group = new NodesGroup(_Network, parameters, new NodeRequirement()
            {
                RequiredServices = NodeServices.Network //Needed for SPV
            });
            _Group.MaximumNodeConnection = config.NodesToConnect;
            _Group.Connect();

            var broadcastManager = new BroadcastManager(_Group);

            walletManager.Start();

            var scanLocation = new BlockLocator();
            var walletBlockLocator = walletManager.GetWalletBlockLocator();
            DateTimeOffset timeToStartOn;

            if (walletBlockLocator != null) //Can be null if a wallet is new
            {
                scanLocation.Blocks.AddRange(walletBlockLocator); // Set starting scan location to wallet's last blockLocator position
                timeToStartOn = chain.GetBlock(walletManager.LastReceivedBlockHash()).Header.BlockTime; //Skip all time before last blockhash synced
            }
            else
            {
                scanLocation.Blocks.Add(_Network.GenesisHash); //Set starting scan location to begining of network chain
                timeToStartOn = walletManager.CreationTime != null ? walletManager.CreationTime : _Network.GetGenesis().Header.BlockTime; //Skip all time before, start of BIP32
            }

            walletSyncManager.Scan(scanLocation, timeToStartOn);

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

            _Logger.Information("Liviano SPV client started");

            WaitUntilEscapeIsPressed(walletManager);
        }

        private static void WaitUntilEscapeIsPressed(WalletManager walletManager)
        {
            bool quit = false;
            bool quitHandledByIDE = false;

            _Logger.Information("Press ESC to stop SPV client...");

            while (!quit)
            {
                try
                {
                    var keyInfo = Console.ReadKey();

                    quit = keyInfo.Key == ConsoleKey.Escape;
                }
                catch (InvalidOperationException e)
                {
                    _Logger.Error("{error}", e);
                    _Logger.Information("Stop handled by IDE", e);

                    quitHandledByIDE = true;
                    quit = true;
                }
            }

            if (quitHandledByIDE)
            {
                Console.ReadLine();

                Cleanup(walletManager);
            }
            else
            {
                Cleanup(walletManager);
                Exit();
            }
        }

        private static void Cleanup(WalletManager walletManager)
        {
            // Include here cleanup stuff, maybe last time to save the file?

            // Example last time to save a file.
            _Logger.Information("Saving chain state...");
            lock(_Lock)
            {
                GetAddressManager().SavePeerFile(AddrmanFile(), _Network);
                using(var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                {
                    GetChain().WriteTo(fs);
                }

                walletManager.SaveWallet();
            }
        }

        private static void Exit()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();

            foreach (ProcessThread thread in process.Threads.OfType<ProcessThread>())
            {
                if (thread.Id == process.Id)
                    continue;

                thread.Dispose();

                _Logger.Information("Closing thread with pid: {pid}, opened for: {timeInSeconds} seconds", thread.Id, thread.UserProcessorTime.TotalSeconds);
            }

            _Logger.Information("Closing thread with pid: {pid}", process.Id);
            _Logger.Information("bye!");

            process.Kill();
        }
    }
}
