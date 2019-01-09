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

        private static ChainedBlock BIP39ActivationMainNet = new ChainedBlock(new BlockHeader("020000005abd8e47d983fee4a20f83f93973d92f072a06c5bc6867640200000000000000b929390f399afa1cc074bb1219be0f6e10a18e338e8ba5b1acfadae86c59d8e01d5dc3520ca3031996821dc7", Network.Main), 277996);
        private static ChainedBlock BIP39ActivationTestNet = new ChainedBlock(new BlockHeader("02000000cc3b4f230127a925da29423cab8974a83b60a5212ce6fd9a30b682e7000000001d153b89315e7eebca2005582395b709a8cce47d626226d53db4a33cad513b8eaa5dc352ffff001d002654ae", Network.TestNet), 154932);

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

        private static ConcurrentChain GetChain()
        {
            lock(_Lock)
            {
                if (_ConParams != null)
                {
                    return _ConParams.TemplateBehaviors.Find<ChainBehavior>().Chain as PartialConcurrentChain;
                }
                var chain = new PartialConcurrentChain(_Network);
                
                using (var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                {
                    ((PartialConcurrentChain)chain).Load(new BitcoinStream(fs,false));
                }

                if (_Network == Network.Main)
                {
                    chain.SetCustomTip(BIP39ActivationMainNet);
                }
                else
                {
                    chain.SetCustomTip(BIP39ActivationTestNet);
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
                        PartialConcurrentChain chain = GetChain() as PartialConcurrentChain;
                        chain.WriteTo(new BitcoinStream(fs,true));
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
            // Include here cleanup stuff, maybe last time to save the file?

            // Example last time to save a file.
            _Logger.Information("Saving chain state...");
            lock(_Lock)
            {
                //GetAddressManager().SavePeerFile(AddrmanFile(), _Network);
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

        private static ChainedBlock GetClosestChainedBlockToDateTimeOffset(DateTimeOffset dateTimeOffset)
        {
            DateTimeOffset creationDate = dateTimeOffset;
            ChainedBlock closestDate = null;
            List<ChainedBlock> theDates = _Network.GetCheckpoints();
            long min = long.MaxValue;

            foreach (ChainedBlock date in theDates)
            {
                if (Math.Abs(date.Header.BlockTime.Ticks - creationDate.Ticks) < min)
                {
                    min = Math.Abs(date.Header.BlockTime.Ticks - creationDate.Ticks);
                    closestDate = date;
                }
            }

            //var indexOfClosestDate = theDates.IndexOf(closestDate);
            //var dateBeforeClosestDate = theDates.ElementAt(indexOfClosestDate - 1);

            return closestDate;
        }

        private static (IAsyncLoopFactory AsyncLoopFactory, IDateTimeProvider DateTimeProvider, IScriptAddressReader ScriptAddressReader, IStorageProvider StorageProvider, WalletManager WalletManager, IWalletSyncManager WalletSyncManager, NodesGroup NodesGroup, NodeConnectionParameters NodeConnectionParameters, IBroadcastManager BroadcastManager) CreateWalletManager(ILogger logger, ConcurrentChain chain, Network network, string walletId, AddressManager addressManager, ScriptTypes scriptTypes = ScriptTypes.SegwitAndLegacy, int maxAmountOfNodes = 4, bool load = true, bool start = true, bool connect = true, bool scan = true, string password = null, DateTimeOffset? timeToStartOn = null)
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

            if (load)
            {
                walletManager.LoadWallet(password);
            }


            var closestDate = GetClosestChainedBlockToDateTimeOffset(walletManager.GetWalletCreationTime());

            if (network == Network.Main)
            {
                chain = new PartialConcurrentChain(BIP39ActivationMainNet);
            }
            else
            {
                chain = new PartialConcurrentChain(BIP39ActivationTestNet);
            }

            nodeConnectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(addressManager));
            nodeConnectionParameters.TemplateBehaviors.Add(new ChainBehavior(chain) { CanRespondToGetHeaders = false , SkipPoWCheck = true});
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
                //TODO Work on logic for starting scan.
                //Consider many things such as, size of chain, wallet block locator, creation date, and so on.
                BlockLocator scanLocation = new BlockLocator();
                ICollection<uint256> walletBlockLocator = walletManager.GetWalletBlockLocator();

                if (walletBlockLocator != null) //If we have scanned before
                {
                    if (!timeToStartOn.HasValue) //If we are NOT passing a value with -d
                    {
                        scanLocation.Blocks.Add(network.GenesisHash);
                        scanLocation.Blocks.AddRange(walletBlockLocator);
                        if (chain.Contains(walletManager.LastReceivedBlockHash()))
                        {
                            timeToStartOn = timeToStartOn ?? chain.GetBlock(walletManager.LastReceivedBlockHash()).Header.BlockTime; //Skip all time before last blockhash synced
                        }
                        else
                        {
                            timeToStartOn = closestDate.Header.BlockTime;
                        }

                    }
                    else
                    {
                        scanLocation.Blocks.Add(network.GenesisHash);
                    }
                }
                else //We have never scanned before
                {
                    scanLocation.Blocks.Add(network.GenesisHash); //Set starting scan location to begining of network chain

                    timeToStartOn = timeToStartOn ?? (walletManager.GetWalletCreationTime() != null ? walletManager.GetWalletCreationTime() : network.GetGenesis().Header.BlockTime); //Skip all time before, start of BIP32
                }
                var blockLocators = new BlockLocator();

                if (network == Network.Main)
                {
                    blockLocators.Blocks.Add(BIP39ActivationMainNet.Header.GetHash());
                    walletSyncManager.Scan(blockLocators, BIP39ActivationMainNet.Header.BlockTime);
                }
                else
                {
                    blockLocators.Blocks.Add(BIP39ActivationTestNet.Header.GetHash());
                    walletSyncManager.Scan(blockLocators, BIP39ActivationTestNet.Header.BlockTime);
                }
            }

            return (asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider, walletManager, walletSyncManager, nodesGroup, nodeConnectionParameters, broadcastManager);
        }
    }
}
