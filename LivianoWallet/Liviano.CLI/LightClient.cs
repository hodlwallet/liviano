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

using Liviano.Exceptions;
using Liviano.Models;
using Liviano.Utilities;
using System.Threading;
using Liviano.Interfaces;

using NBitcoin.DataEncoders;
using System.Reflection;
using Newtonsoft.Json;
using Liviano.Electrum;
using System.ComponentModel.DataAnnotations;
using Liviano.Extensions;
using Serilog.Core;
using Liviano.Bips;
using System.Data.Common;
using Liviano.Storages;

namespace Liviano.CLI
{
    public static class LightClient
    {
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

        public static async Task<(bool WasCreated, bool WasSent, Transaction Tx, string Error)> Send(Config config, string password, string destinationAddress, double amount, int satsPerByte, string accountName = null, string accountIndex = null)
        {
            throw new NotImplementedException("TODO");
        }

        public static (string Name, string HdPath, Money ConfirmedAmount, Money UnConfirmedAmount) AccountBalance(Config config, string password, string accountName = null, string accountIndex = null)
        {
            throw new NotImplementedException("TODO");
        }

        public static IEnumerable<(string Name, string HdPath, Money ConfirmedAmount, Money UnConfirmedAmount)> AllAccountsBalance(Config config, string password)
        {
            throw new NotImplementedException("TODO");
        }

        public static BitcoinAddress GetAddress(Config config, string password, string accountIndex = null, string accountName = null)
        {
            throw new NotImplementedException("TODO");
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
            _Network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemStorage(config.WalletId, _Network);

            if (!storage.Exists())
            {
                Console.WriteLine($"Wallet {config.WalletId} doesn't exists.");

                return;
            }

            _Wallet = storage.Load();

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

            w.Init(mnemonic, "", network: Network.TestNet);

            //w.AddAccount("paper", options: new { Network = Network.TestNet });
            w.AddAccount("bip141");
            var account = w.Accounts[0].CastToAccountType();

            Console.WriteLine($"Account Type: {account.GetType()}");
            Console.WriteLine($"Added account with path: {account.HdPath}");

            w.Storage.Save();

            Console.WriteLine("Saved Wallet!");

            if (account.AccountType == "paper")
            {
                var addr = account.GetReceiveAddress();
                Console.WriteLine($"{addr.ToString()} => scriptHash: {addr.ToScriptHash().ToHex()}");
            }
            else
            {
                int n = account.GapLimit;
                Console.WriteLine($"Addresses ({n})");

                foreach (var addr in account.GetReceiveAddress(n))
                {
                    Console.WriteLine($"{addr.ToString()} => scriptHash: {addr.ToScriptHash().ToHex()}");
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

                w.Storage.Save();

                await w.Start();
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
            var process = System.Diagnostics.Process.GetCurrentProcess();

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
