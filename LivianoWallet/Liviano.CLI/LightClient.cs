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
                    return _ConParams.TemplateBehaviors.Find<ChainBehavior>().Chain as PartialConcurrentChain;
                }
                var chain = new PartialConcurrentChain(_Network);
                //using(var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                //{
                //    chain.Load(fs);
                //}

                chain.SetCustomTip(GetCheckpoints().First());


                return chain;
            }
        }

        private  static List<ChainedBlock> GetCheckpoints()
        {
            return new List<ChainedBlock>() {
                //Block Hex - 020000001939e922692d67e9da0c512082b3caaebaf04fac89499b07f310af000000000022c4fd8dd050b04bac685e24a0d0d6d21101ad605bc9effed2a654016e903836b2640c5207d9001cda8a7fb70401000000010000000000000000000000000000000000000000000000000000000000000000ffffffff3703c08901000407d9001c0417570000522cfabe6d6d0000000000000000000068692066726f6d20706f6f6c7365727665726aac1eeeed88ffffffff01a078072a010000001976a914912e2b234f941f30b18afbb4fa46171214bf66c888ac0000000001000000012bde870a76d5864149993d3379247e02df18639f54459d0b639e938cc579b5b6010000006a47304402200a0309dd1c592291942eebc10a44b5afe15acb0038bf0496f24a6d965ee3485b02206b1dad3c1191afbb717722490492f2f2214c9af6c2d21f664fffc2d4c00a5aac012102221b1343d47a4660a21d25bcc89361fde6f8624d1dd829560b9b4fe5e91a45a7ffffffff02e0834e02000000001976a9146953ce65058e5e68125a9163d74b277d6a7f4a9e88aca02b7208000000001976a91402613aca07110cd9dce0a1633f305ca8e5a8dc2e88ac000000000100000001fd8bc1898c487c3993fb2122d62a7aff551d4d14d6ad0cbe753ca635ec3accb4010000006b483045022100ae0278870da3300a89cbb653d2e05d7fe9ff17fd81b5276962f2278ef7731a3a022051be5081def0bfb5be719642028a042dbbe106607f318d7171664c49bab2fcfe0121035292bf8e7fe116c5acfbb6b6d2e546c6c0a7ff30d4cc4e87a0555fcf3aafdfa1ffffffff0290c04d02000000001976a9146953ce65058e5e68125a9163d74b277d6a7f4a9e88ace0e8cf02000000001976a91422170ca0a180d1cbefc4e45598aed3a2de05778088ac00000000010000000166405a0fbfaabfb855d83d93510f7321c92ff0b97ebeefcb81364b047ec8ffbb010000006c493046022100bca8d0963d3c01508a5f9bbf8093007f18fbb196227a34cbcec8c7e879018088022100e68ebd50155ddaa3359445928d7d6494e039bba4f78e926c10bd38b19d2b16ea012102221b1343d47a4660a21d25bcc89361fde6f8624d1dd829560b9b4fe5e91a45a7ffffffff0290c04d02000000001976a9146953ce65058e5e68125a9163d74b277d6a7f4a9e88ace0150104000000001976a91402613aca07110cd9dce0a1633f305ca8e5a8dc2e88ac00000000
               new ChainedBlock(new BlockHeader(new byte[]{ 2,0,0,0,25,57,233,34,105,45,103,233,218,12,81,32,130,179,202,174,186,240,79,172,137,73,155,7,243,16,175,0,0,0,0,0,34,196,253,141,208,80,176,75,172,104,94,36,160,208,214,210,17,1,173,96,91,201,239,254,210,166,84,1,110,144,56,54,178,100,12,82,7,217,0,28,218,138,127,183}),100800),

            };   
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

            walletManager.CreateWallet(password, config.WalletId, WalletManager.MnemonicFromString(mnemonic));
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
                GetAddressManager().SavePeerFile(AddrmanFile(), _Network);
                using(var fs = File.Open(ChainFile(), FileMode.OpenOrCreate))
                {
                    //GetChain().WriteTo(fs);
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

            if (load && password != null)
            {
                walletManager.LoadWallet(password);
            }

            nodeConnectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(addressManager));
            nodeConnectionParameters.TemplateBehaviors.Add(new ChainBehavior(chain) { CanRespondToGetHeaders = false });
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
                ICollection<uint256> walletBlockLocator = walletManager.GetWalletBlockLocator();

                if (walletBlockLocator != null) //If we have scanned before
                {
                    if (!timeToStartOn.HasValue) //If we are NOT passing a value with -d
                    {
                        scanLocation.Blocks.AddRange(walletBlockLocator);
                        timeToStartOn = timeToStartOn ?? chain.GetBlock(walletManager.LastReceivedBlockHash()).Header.BlockTime; //Skip all time before last blockhash synced
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
                walletSyncManager.Scan(scanLocation, timeToStartOn.Value);
            }

            return (asyncLoopFactory, dateTimeProvider, scriptAddressReader, storageProvider, walletManager, walletSyncManager, nodesGroup, nodeConnectionParameters, broadcastManager);
        }
    }
}
