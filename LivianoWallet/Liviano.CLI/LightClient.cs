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
using System.Threading;
using Liviano.Interfaces;
using Liviano.Enums;

namespace Liviano.CLI
{
    public class LightClient
    {
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
                await Task.Delay(30_000);

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

        private static PartialConcurrentChain GetChain()
        {
            lock(_Lock)
            {
                if (_ConParams != null)
                {
                    return _ConParams.TemplateBehaviors.Find<PartialChainBehavior>().Chain as PartialConcurrentChain;
                }

                var chain = new PartialConcurrentChain(_Network);

                using (var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                {
                    chain.Load(new BitcoinStream(fs, false));
                }

                if (chain.Tip.Height < _Network.GetBIP39ActivationChainedBlock().Height)
                    chain.SetCustomTip(_Network.GetBIP39ActivationChainedBlock());

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
                        PartialConcurrentChain chain = GetChain();
                        chain.WriteTo(new BitcoinStream(fs, true));
                    }
                }
            });
        }

        public static async Task<(bool WasCreated, bool WasSent, Transaction Tx, string Error)> Send(Config config, string password, string destinationAddress, double amount, int satsPerByte, string accountName = null, string accountIndex = null)
        {
            _Network = HdOperations.GetNetwork(config.Network);

            var chain = GetChain();
            var addressManager = GetAddressManager();
            var result = CreateWalletManager(
                _Logger,
                chain,
                _Network,
                config.WalletId,
                addressManager,
                maxAmountOfNodes: config.NodesToConnect,
                password: password,
                scan: true
            );

            _Logger.Information("Loading wallet: {walletFileId} on {network}", config.WalletId, _Network.Name);

            WalletManager walletManager = result.WalletManager;
            var broadcastManager = result.BroadcastManager;
            var coinSelector = new DefaultCoinSelector();
            var transactionManager = new TransactionManager(broadcastManager, walletManager, coinSelector, chain);
            var btcAmount = new Money(new Decimal(amount), MoneyUnit.BTC);

            HdAccount account = null;
            if (accountIndex == null && accountName == null)
            {
                account = walletManager.GetAllAccountsByCoinType(CoinType.Bitcoin).First();
            }
            else if (accountIndex != null)
            {
                account = walletManager.GetAllAccountsByCoinType(CoinType.Bitcoin).First(a => a.Index == int.Parse(accountIndex));
            }
            else if (accountName != null)
            {
                account = walletManager.GetAllAccountsByCoinType(CoinType.Bitcoin).First(a => a.Name == accountName);
            }

            Transaction tx = null;
            string error = "";
            bool wasCreated = false;
            bool wasSent = false;

            try
            {
                tx = transactionManager.CreateTransaction(destinationAddress, btcAmount, satsPerByte, account, password);
                wasCreated = true;
            }
            catch (WalletException e)
            {
                _Logger.Error(e.ToString());

                error = e.Message;
                wasCreated = false;

                return (wasCreated, wasSent, tx, error);
            }

            transactionManager.VerifyTransaction(tx, out var errors);

            if (errors.Count() > 0)
            {
                error = String.Join<string>(", ", errors.Select(o => o.Message));
            }

            if (wasCreated)
            {
                Thread.Sleep(30000);

                await transactionManager.BroadcastTransaction(tx);
                wasSent = true;
            }
            return (wasCreated, wasSent, tx, error);
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
                _Logger.Error(e.Message);

                throw new WalletException($"Could not find account index ({accountIndex}) or name ({accountName})");
            }
        }

        public static IEnumerable<(string Name, string HdPath, Money ConfirmedAmount, Money UnConfirmedAmount)> AllAccountsBalance(Config config, string password)
        {
            List<(string, string, Money, Money)> balances = new List<(string, string, Money, Money)>();

            _Network = HdOperations.GetNetwork(config.Network);

            WalletManager walletManager = new WalletManager(_Logger, _Network, config.WalletId);

            if (!walletManager.GetStorageProvider().WalletExists())
            {
                _Logger.Error("Error loading wallet wallet from {walletId}", config.WalletId);

                throw new WalletException($"Error loading wallet from wallet id");
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

            HdAccount account = null;
            WalletManager walletManager = new WalletManager(_Logger, _Network, config.WalletId);

            walletManager.LoadWallet(password);

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

            _Logger.Information("Creating wallet for file: {walletFileId} on {network}", config.WalletId, _Network.Name);

            WalletManager walletManager = new WalletManager(_Logger, _Network, config.WalletId);

            walletManager.CreateWallet(config.WalletId, password, WalletManager.MnemonicFromString(mnemonic));
        }

        public static void Start(Config config, string password, string datetime = null, bool dropTransactions = false)
        {
            _Network = HdOperations.GetNetwork(config.Network);

            var result = CreateWalletManager(
                _Logger,
                GetChain(),
                _Network,
                config.WalletId,
                GetAddressManager(),
                maxAmountOfNodes: config.NodesToConnect,
                password: password,
                timeToStartOn: datetime == null ? new DateTimeOffset?() : DateTimeOffset.Parse(datetime)
            );

            WalletManager walletManager = result.WalletManager;

            if (dropTransactions)
            {
                var transactionsRemoved = walletManager.RemoveAllTransactions();

                foreach (var item in transactionsRemoved)
                {
                    Console.WriteLine($"Deleting Tx Id: {item.Id}");
                    Console.WriteLine($"Propagated: {item.IsPropagated}");
                }
            }

            var walletSyncManager = result.WalletSyncManager;
            var parameters = result.NodeConnectionParameters;

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

            _Logger.Information("Liviano SPV client started.");

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

                    if (keyInfo.Key == ConsoleKey.Enter)
                        Console.WriteLine();

                    quit = keyInfo.Key == ConsoleKey.Escape;
                }
                catch (InvalidOperationException e)
                {
                    _Logger.Error("{error}", e.Message);
                    _Logger.Information("Stop handled by IDE");

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
            lock(_Lock)
            {
                GetAddressManager().SavePeerFile(AddrmanFile(), _Network);
                using(var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                {
                    PartialConcurrentChain chain = GetChain();

                    chain.WriteTo(new BitcoinStream(fs, true));
                }
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

        private static ChainedBlock GetClosestChainedBlockToDateTimeOffset(DateTimeOffset creationDate)
        {
            return _Network.GetCheckpoints().OrderBy(chainedBlock => Math.Abs(chainedBlock.Header.BlockTime.Ticks - creationDate.Ticks)).FirstOrDefault();
        }

        private static (IAsyncLoopFactory AsyncLoopFactory, IDateTimeProvider DateTimeProvider, IScriptAddressReader ScriptAddressReader, IStorageProvider StorageProvider, WalletManager WalletManager, IWalletSyncManager WalletSyncManager, NodesGroup NodesGroup, NodeConnectionParameters NodeConnectionParameters, IBroadcastManager BroadcastManager) CreateWalletManager(ILogger logger, PartialConcurrentChain chain, Network network, string walletId, AddressManager addressManager, ScriptTypes scriptTypes = ScriptTypes.SegwitAndLegacy, int maxAmountOfNodes = 4, bool load = true, bool start = true, bool connect = true, bool scan = true, string password = null, DateTimeOffset? timeToStartOn = null)
        {
            AsyncLoopFactory asyncLoopFactory = new AsyncLoopFactory();
            DateTimeProvider dateTimeProvider = new DateTimeProvider();
            ScriptAddressReader scriptAddressReader = new ScriptAddressReader();
            FileSystemStorageProvider storageProvider = new FileSystemStorageProvider(walletId);
            NodeConnectionParameters nodeConnectionParameters = new NodeConnectionParameters();

            WalletManager walletManager = new WalletManager(logger, network, chain, asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider);

            logger.Information("Loading wallet: {walletFileId} on {network}", walletId, network.Name);

            WalletSyncManager walletSyncManager = new WalletSyncManager(logger, walletManager, chain);

            if (!storageProvider.WalletExists())
            {
                logger.Error("Error loading wallet from {walletId}", walletId);

                throw new WalletException($"Error loading wallet from wallet id: {walletId}");
            }

            ChainedBlock closestChainedBlock = null;
            if (load && walletManager.LoadWallet(password))
            {
                logger.Information($"Loaded wallet, with id {walletId}");

                closestChainedBlock = GetClosestChainedBlockToDateTimeOffset(walletManager.GetWalletCreationTime());

                if (chain.Tip.Header.BlockTime < closestChainedBlock.Header.BlockTime)
                    chain.SetCustomTip(closestChainedBlock);
            }

            nodeConnectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(addressManager));
            nodeConnectionParameters.TemplateBehaviors.Add(new PartialChainBehavior(chain) { CanRespondToGetHeaders = false , SkipPoWCheck = true});
            nodeConnectionParameters.TemplateBehaviors.Add(new WalletSyncManagerBehavior(logger, walletSyncManager, scriptTypes));
            NodesGroup nodesGroup = new NodesGroup(network, nodeConnectionParameters, new NodeRequirement() {
                RequiredServices = NodeServices.Network
            });

            BroadcastManager broadcastManager = new BroadcastManager(nodesGroup);

            nodeConnectionParameters.TemplateBehaviors.Add(new TransactionBroadcastBehavior(broadcastManager));

            nodesGroup.NodeConnectionParameters = nodeConnectionParameters;

            if (connect)
            {
                nodesGroup.MaximumNodeConnection = maxAmountOfNodes;
                nodesGroup.Connect();
            }

            if (start)
            {
                walletManager.Start();
            }

            if (scan)
            {
                BlockLocator scanLocation = new BlockLocator();
                DateTimeOffset dateToStartScanning;
                ICollection<uint256> walletBlockLocator = walletManager.GetWalletBlockLocator();

                if (walletBlockLocator != null)
                {
                    scanLocation.Blocks.AddRange(walletBlockLocator);
                }
                else
                {
                    scanLocation.Blocks.Add(network.GetBIP39ActivationChainedBlock().HashBlock);
                }

                if (timeToStartOn.HasValue)
                {
                    dateToStartScanning = timeToStartOn.Value;
                }
                else if (closestChainedBlock.Header.BlockTime > chain.Tip.Header.BlockTime)
                {
                    dateToStartScanning = closestChainedBlock.Header.BlockTime;
                }
                else
                {
                    dateToStartScanning = network.GetBIP39ActivationChainedBlock().Header.BlockTime;
                }

                walletSyncManager.Scan(scanLocation, dateToStartScanning);
            }

            return (asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider, walletManager, walletSyncManager, nodesGroup, nodeConnectionParameters, broadcastManager);
        }
    }
}
