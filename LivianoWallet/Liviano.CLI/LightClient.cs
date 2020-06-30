using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using Newtonsoft.Json;
using NBitcoin;
using Serilog;

using Liviano.Exceptions;
using Liviano.Models;
using Liviano.Interfaces;
using Liviano.Electrum;
using Liviano.Extensions;
using Liviano.Bips;
using Liviano.Storages;

namespace Liviano.CLI
{
    public static class LightClient
    {
        const int PERIODIC_SAVE_DELAY = 30_000;

        static readonly object @lock = new object();

        static readonly ILogger logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        static Network network;

        static IWallet wallet;

        static async Task PeriodicSave()
        {
            while (true)
            {
                await Save();

                await Task.Delay(PERIODIC_SAVE_DELAY);
            }
        }

        static async Task Save()
        {
            await Task.Factory.StartNew(() =>
            {
                lock (@lock)
                {
                    wallet.Storage.Save();
                }
            });
        }

        public static Wallet NewWalletFromMnemonic(string mnemonic, Network network)
        {
            var wallet = new Wallet();

            wallet.Init(mnemonic: mnemonic, network: network);

            return wallet;
        }

        public static Wallet NewWallet(string wordlist, int wordCount, Network network)
        {
            var mnemonic = Hd.NewMnemonic(wordlist, wordCount).ToString();

            return NewWalletFromMnemonic(mnemonic, network);
        }

        static void LoadWallet(Config config)
        {
            network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemStorage(config.WalletId, network);

            if (!storage.Exists())
            {
                Console.WriteLine($"Wallet {config.WalletId} doesn't exists.");

                throw new WalletException("Invalid wallet id");
            }

            wallet = storage.Load();
        }

        public static async Task<(bool WasCreated, bool WasSent, Transaction Tx, string Error)> Send(Config config, string password, string destinationAddress, double amount, int satsPerByte, string accountName = null, string accountIndex = null)
        {
            LoadWallet(config);

            throw new NotImplementedException("TODO");
        }

        public static async Task<(bool WasCreated, bool WasSent, Transaction Tx, string Error)> Send(Config config, string password, string destinationAddress, double amount, int satsPerByte, IAccount account)
        {
            LoadWallet(config);

            Transaction tx = null;
            string error = "";
            bool wasCreated = false;
            bool wasSent = false;
            var txAmount = new Money(new Decimal(amount), MoneyUnit.BTC);

            try
            {
                tx = TransactionExtensions.CreateTransaction(password, destinationAddress, txAmount, (long)satsPerByte, wallet, account, network);
                wasCreated = true;
            }
            catch (WalletException e)
            {
                logger.Error(e.ToString());

                error = e.Message;
                wasCreated = false;

                return (wasCreated, wasSent, tx, error);
            }

            TransactionExtensions.VerifyTransaction(tx, network, out var errors);

            if (errors.Any())
            {
                error = string.Join<string>(", ", errors.Select(o => o.Message));
            }

            //if (wasCreated)
            //{
            //    Thread.Sleep(SEND_PAUSE);

            //    try
            //    {
            //        var txHex = tx.ToHex();

            //        var electrumClient = new ElectrumClient(ElectrumClient.GetRecentlyConnectedServers());
            //        var broadcast = await electrumClient.BlockchainTransactionBroadcast(txHex);

            //        if (broadcast.Result != tx.GetHash().ToString())
            //        {
            //            throw new ElectrumException($"Transaction broadcast failed for tx: {txHex}");
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        _Logger.Error(e.ToString());

            //        error = e.Message;
            //        wasSent = false;

            //        return (wasCreated, wasSent, tx, error);
            //    }

            //    wasSent = true;
            //}

            return (wasCreated, wasSent, tx, error);
        }

        public static Money AccountBalance(Config config, string accountName = null, string accountIndex = null)
        {
            LoadWallet(config);

            throw new NotImplementedException("TODO");
        }

        public static Dictionary<IAccount, Money> AllAccountsBalances(Config config)
        {
            LoadWallet(config);

            throw new NotImplementedException("TODO");
        }

        public static BitcoinAddress GetAddress(Config config, string password, string accountIndex = null, string accountName = null)
        {
            network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemStorage(config.WalletId, network);

            if (!storage.Exists())
            {
                Console.WriteLine($"Wallet {config.WalletId} doesn't exists.");

                return null;
            }

            wallet = storage.Load();

            IAccount account;
            if (accountName is null)
            {
                account = wallet.Accounts[int.Parse(accountIndex)];
            }
            else
            {
                account = wallet.Accounts.FirstOrDefault((i) => i.Name == accountName);
            }

            if (account is null) return null;

            return account.GetReceiveAddress();
        }

        public static void CreateWallet(Config config, string password, string mnemonic)
        {
            network = Hd.GetNetwork(config.Network);

            if (password == null)
                password = "";

            logger.Information("Creating wallet for file: {walletFileId} on {network}", config.WalletId, network.Name);

            wallet = new Wallet { Id = config.WalletId };

            wallet.Init(mnemonic, password, network: network);

            wallet.Storage.Save();
        }

        public static void Start(Config config, bool resync = false)
        {
            LoadWallet(config);

            wallet.SyncStarted += (s, e) =>
            {
                logger.Information("Sync started!");
            };

            wallet.SyncFinished += (s, e) =>
            {
                logger.Information("Sync finished!");
            };

            if (resync) wallet.Resync();
            else wallet.Sync();

            wallet.Start();

            _ = PeriodicSave();

            WaitUntilEscapeIsPressed();
        }

        /// <summary>
        /// Adds an account to the wallet and returns its xpub
        /// </summary>
        /// <param name="config"><see cref="Config"/> to load the wallet from</param>
        /// <param name="type">Type of the account</param>
        /// <param name="name">Name of the account</param>
        /// <returns>An xpub of the account</returns>
        public static string AddAccount(Config config, string type, string name)
        {
            LoadWallet(config);

            wallet.AddAccount(type, name, new { Wallet = wallet, WalletId = wallet.Id, Network = wallet.Network });

            wallet.Storage.Save();

            config.Network = wallet.Network.ToString();
            config.AddWallet(wallet.Id);

            config.SaveChanges();

            return wallet.CurrentAccount.ExtendedPubKey;
        }

        public static void TestElectrumConnection3(Network network)
        {
            logger.Information("Try to connect to each electrum server manually");
            logger.Information($"Running on {network}");

            string serversFileName = ElectrumPool.GetLocalConfigFilePath(
                "Electrum",
                "servers",
                $"{network.Name.ToLower()}.json"
            );

            var json = File.ReadAllText(serversFileName);
            var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            var servers = ElectrumServers.FromDictionary(data).Servers.CompatibleServers();

            var pool = new ElectrumPool(servers.ToArray());

            pool.OnConnectedEvent += Pool_OnConnectedEvent;
            pool.OnDoneFindingPeersEvent += Pool_OnDoneFindingPeersEvent;
            pool.OnCancelFindingPeersEvent += Pool_OnCancelFindingPeersEvent;

            var cts = new CancellationTokenSource();
            _ = pool.FindConnectedServers(cts);

            // Use this code to cancel
            //Thread.Sleep(1000);
            //cts.Cancel();

            WaitUntilEscapeIsPressed();
        }

        public static void WalletTest1(Network network, string wordlist, int wordCount)
        {
            var wallet = NewWallet(wordlist, wordCount, network);
        }

        static void Pool_OnCancelFindingPeersEvent(object sender, EventArgs e)
        {
            Console.WriteLine("\n!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine($"Cancelled finding peers at {DateTime.UtcNow}");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!\n");
        }

        static void Pool_OnDoneFindingPeersEvent(object sender, EventArgs e)
        {
            Console.WriteLine("\n!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine($"Done finding peers at {DateTime.UtcNow}");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!\n");

            var pool = (ElectrumPool)sender;

            Console.WriteLine($"Current server: {pool.CurrentServer.Domain}:{pool.CurrentServer.PrivatePort}");
            Console.WriteLine("Other servers connected: \n");

            foreach (var s in pool.ConnectedServers)
            {
                Console.WriteLine($"{s.Domain}:{s.PrivatePort}");
            }
        }

        static void Pool_OnConnectedEvent(object sender, Server e)
        {
            Console.WriteLine("\n!!!!!!!!!!!!!!!!!!!!!!!!!!");
            Console.WriteLine($"First Server to Connect!\n{e.Domain} at {DateTime.UtcNow}");
            Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!\n");
        }

        public static void TestElectrumConnection2(Network network)
        {
            logger.Information("Try to connect to each electrum server manually");
            logger.Information($"Running on {network}");

            string serversFileName = ElectrumPool.GetLocalConfigFilePath(
                "Electrum",
                "servers",
                $"{network.Name.ToLower()}.json"
            );

            var json = File.ReadAllText(serversFileName);
            var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            var servers = ElectrumServers.FromDictionary(data).Servers.CompatibleServers();

            var tasks = new List<Task> { };
            foreach (var s in servers)
            {
                Console.WriteLine($"Connecting to {s.Domain}:{s.PrivatePort}...");

                var electrum = s.ElectrumClient;

                var t = Task.Run(async () =>
                {
                    try
                    {
                        Debug.WriteLine($"Got in! at {DateTime.UtcNow}");
                        var res = await electrum.ServerVersion();

                        Debug.WriteLine($"Server: {s.Domain}:{s.PrivatePort}, Res = {res}");

                        Debug.WriteLine($"Done! at {DateTime.UtcNow}");

                        return (s, res.ToString(), false);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Server: {s.Domain}:{s.PrivatePort}, Error = {e.Message}");

                        return (s, e.Message, true);
                    }
                });

                tasks.Add(t);
            }

            var tarray = tasks.ToArray();

            Task.WaitAll(tasks.ToArray());

            foreach (var t in tarray)
            {
                var ct = (Task<ValueTuple<Liviano.Models.Server, string, bool>>)t;

                var domain = ct.Result.Item1.Domain;
                var content = ct.Result.Item2;
                var hasError = ct.Result.Item3;

                if (hasError)
                {
                    Console.WriteLine($"ERROR!     {domain} => {content}");
                    continue;
                }

                Console.WriteLine($"CONNECTED! {domain} => {content}");
            }

            WaitUntilEscapeIsPressed();
        }

        public static void TestElectrumConnection(string address, string txHash, Network network = null)
        {
            if (network is null) network = Network.Main;

            logger.Information("Running on {network}", network.Name);
            logger.Information("Getting address balance from: {address} and tx details from: {txHash}", address, txHash);

            Console.WriteLine("Welcome to a demo");

            Console.WriteLine("Delete previous wallets");

            if (Directory.Exists("wallets"))
                Directory.Delete("wallets", recursive: true);

            //Console.WriteLine(contents);
            var mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

            Console.WriteLine($"Creating wallet for mnemonic: \"{mnemonic}\"");

            var w = new Wallet();

            w.Init(mnemonic, "", network: network);
            //w.AddAccount("paper", options: new { Network = Network.TestNet });
            w.AddAccount("bip141", options: new { Network = network });
            var account = w.Accounts[0].CastToAccountType();

            Console.WriteLine($"Account Type: {account.GetType()}");
            Console.WriteLine($"Added account with path: {account.HdPath}");

            w.Storage.Save();

            Console.WriteLine("Saved Wallet!");

            if (account.AccountType == "paper")
            {
                var addr = account.GetReceiveAddress();
                Console.WriteLine($"{addr} => scriptHash: {addr.ToScriptHash().ToHex()}");
            }
            else
            {
                int n = account.GapLimit;
                Console.WriteLine($"Addresses ({n})");

                foreach (var addr in account.GetReceiveAddress(n))
                {
                    Console.WriteLine($"{addr} => scriptHash: {addr.ToScriptHash().ToHex()}");
                }

                account.ExternalAddressesCount = 0;
            }

            Console.WriteLine("\nPress [ESC] to stop!\n");

            Console.WriteLine("Syncing...");

            var start = new DateTimeOffset();
            var end = new DateTimeOffset();
            w.SyncStarted += (obj, _) =>
            {
                start = DateTimeOffset.UtcNow;

                Console.WriteLine($"Syncing started at {start.LocalDateTime.ToLongTimeString()}");
            };

            w.SyncFinished += (obj, _) =>
            {
                end = DateTimeOffset.UtcNow;

                Console.WriteLine($"Syncing ended at {end.LocalDateTime.ToLongTimeString()}");
                Console.WriteLine($"Syncing time: {(end - start).TotalSeconds}");

                Console.WriteLine($"Balance: {w.Accounts[0].GetBalance()}");

                w.Storage.Save();

                // await w.Start();
            };

            _ = w.Sync();

            Console.WriteLine("Started, now listening to txs");


            WaitUntilEscapeIsPressed();
        }

        static void WaitUntilEscapeIsPressed()
        {
            bool quit = false;
            bool quitHandledByIDE = false;

            logger.Information("Press {key} to stop Liviano...", "ESC");

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
                    logger.Error("{error}", e.Message);
                    logger.Information("Stop handled by IDE");

                    quitHandledByIDE = true;
                    quit = true;
                }
            }

            if (quitHandledByIDE)
            {
                Console.ReadLine();
            }
            else
            {
                Exit();
            }
        }

        static void Exit()
        {
            var process = Process.GetCurrentProcess();

            foreach (ProcessThread thread in process.Threads.OfType<ProcessThread>())
            {
                if (thread.Id == process.Id)
                    continue;

                thread.Dispose();

                if (thread.ThreadState == System.Diagnostics.ThreadState.Terminated)
                    logger.Information("Closing thread with pid: {pid}, opened for: {timeInSeconds} seconds", thread.Id, thread.UserProcessorTime.TotalSeconds);
            }

            logger.Information("Closing thread with pid: {pid}", process.Id);
            logger.Information("bye!");

            process.Kill();
        }

        static ChainedBlock GetClosestChainedBlockToDateTimeOffset(DateTimeOffset creationDate)
        {
            return network.GetCheckpoints().OrderBy(chainedBlock => Math.Abs(chainedBlock.Header.BlockTime.Ticks - creationDate.Ticks)).FirstOrDefault();
        }
    }
}
