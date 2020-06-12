using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;

using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Serilog;

using Liviano.Exceptions;
using Liviano.Models;
using Liviano.Utilities;
using System.Threading;
using Liviano.Interfaces;
using Liviano.Electrum;

using NBitcoin.DataEncoders;
using System.Reflection;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using Liviano.Extensions;
using Serilog.Core;
using Liviano.Bips;
using System.Data.Common;
using Liviano.Storages;
using System.Runtime.CompilerServices;
using System.Runtime;

namespace Liviano.CLI
{
    public static class LightClient
    {
        const int SEND_PAUSE = 30000;

        private static object _Lock = new object();

        private static ILogger _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        private static Network _Network;

        private static IWallet _Wallet;

        private static async Task PeriodicSave()
        {
            while (true)
            {
                await Save();

                await Task.Delay(30_000);
            }
        }

        private static async Task Save()
        {
            await Task.Factory.StartNew(() =>
            {
                lock (_Lock)
                {
                    _Wallet.Storage.Save();
                }
            });
        }

        private static void LoadWallet(Config config)
        {
            _Network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemStorage(config.WalletId, _Network);

            if (!storage.Exists())
            {
                Console.WriteLine($"Wallet {config.WalletId} doesn't exists.");

                throw new WalletException("Invalid wallet id");
            }

            _Wallet = storage.Load();
        }

        public static void ShowHelp()
        {
            Console.WriteLine("Liviano is a wallet.");
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
                tx = TransactionExtensions.CreateTransaction(password, destinationAddress, txAmount, (long)satsPerByte, _Wallet, account, _Network);
                wasCreated = true;
            }
            catch (WalletException e)
            {
                _Logger.Error(e.ToString());

                error = e.Message;
                wasCreated = false;

                return (wasCreated, wasSent, tx, error);
            }

            TransactionExtensions.VerifyTransaction(tx, _Network, out var errors);

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
            _Network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemStorage(config.WalletId, _Network);

            if (!storage.Exists())
            {
                Console.WriteLine($"Wallet {config.WalletId} doesn't exists.");

                return null;
            }

            _Wallet = storage.Load();

            IAccount account;
            if (accountName is null)
            {
                account = _Wallet.Accounts[int.Parse(accountIndex)];
            }
            else
            {
                account = _Wallet.Accounts.FirstOrDefault((i) => i.Name == accountName);
            }

            if (account is null) return null;

            return account.GetReceiveAddress();
        }

        public static void CreateWallet(Config config, string password, string mnemonic)
        {
            _Network = Hd.GetNetwork(config.Network);

            if (password == null)
                password = "";

            _Logger.Information("Creating wallet for file: {walletFileId} on {network}", config.WalletId, _Network.Name);

            _Wallet = new Wallet { Id = config.WalletId };

            _Wallet.Init(mnemonic, password, network: _Network);

            _Wallet.Storage.Save();
        }

        public static void Start(Config config, bool resync = false)
        {
            LoadWallet(config);

            _Wallet.SyncStarted += (s, e) =>
            {
                _Logger.Information("Sync started!");
            };

            _Wallet.SyncFinished += (s, e) =>
            {
                _Logger.Information("Sync finished!");
            };

            if (resync) _Wallet.Resync();
            else _Wallet.Sync();

            _Wallet.Start();

            _ = PeriodicSave();

            WaitUntilEscapeIsPressed();
        }

        public static async void TestElectrumConnection3(Network network)
        {
            _Logger.Information("Try to connect to each electrum server manually");
            _Logger.Information($"Running on {network}");

            string serversFileName = ElectrumClient.GetLocalConfigFilePath(
                "Electrum",
                "servers",
                $"{network.Name.ToLower()}.json"
            );

            var json = File.ReadAllText(serversFileName);
            var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            var servers = ElectrumServers.FromDictionary(data).Servers.CompatibleServers();

            var pool = new ElectrumPool(servers.ToArray());

            pool.OnConnectedEvent += Pool_OnConnectedEvent;

            pool.FindConnectedServers();

            WaitUntilEscapeIsPressed();
        }

        private static void Pool_OnConnectedEvent(object sender, Server e)
        {
            Console.WriteLine($"Connected to {e.Domain}");
        }

        public static void TestElectrumConnection2(Network network)
        {
            _Logger.Information("Try to connect to each electrum server manually");
            _Logger.Information($"Running on {network}");

            string serversFileName = ElectrumClient.GetLocalConfigFilePath(
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
                        var res = await electrum.ServerVersion(ElectrumClient.CLIENT_NAME, ElectrumClient.REQUESTED_VERSION);

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

            _Logger.Information("Running on {network}", network.Name);
            _Logger.Information("Getting address balance from: {address} and tx details from: {txHash}", address, txHash);

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

            w.SyncFinished += async (obj, _) =>
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

        private static void WaitUntilEscapeIsPressed()
        {
            bool quit = false;
            bool quitHandledByIDE = false;

            _Logger.Information("Press {key} to stop Liviano...", "ESC");

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
            }
            else
            {
                Exit();
            }
        }

        private static void Exit()
        {
            var process = Process.GetCurrentProcess();

            foreach (ProcessThread thread in process.Threads.OfType<ProcessThread>())
            {
                if (thread.Id == process.Id)
                    continue;

                thread.Dispose();

                if (thread.ThreadState == System.Diagnostics.ThreadState.Terminated)
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
    }
}
