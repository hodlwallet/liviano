//
// LightClient.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
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

        static void Load(Config config)
        {
            network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemStorage(config.WalletId, network);

            if (!storage.Exists())
            {
                Console.WriteLine($"[Load] Wallet {config.WalletId} doesn't exists. Make sure you're on the right network");

                throw new WalletException("Invalid wallet id");
            }

            wallet = storage.Load();
        }

        public static async Task<(bool WasCreated, bool WasSent, Transaction Tx, string Error)> Send(Config config, string password, string destinationAddress, double amount, int satsPerByte, string accountName = null, string accountIndex = null)
        {
            Load(config);

            await Task.Delay(1);

            throw new NotImplementedException("TODO");
        }

        public static async Task<(bool WasCreated, bool WasSent, Transaction Tx, string Error)> Send(Config config, string password, string destinationAddress, double amount, int satsPerByte, IAccount account)
        {
            Load(config);

            await Task.Delay(1);

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

            return (wasCreated, wasSent, tx, error);
        }

        public static Money AccountBalance(Config config, string accountName = null, int accountIndex = -1)
        {
            Load(config);

            IAccount account;

            if (accountName != null)
                account = wallet.Accounts.Where(acc => acc.Name == accountName).FirstOrDefault();
            else if (accountIndex != -1)
                account = wallet.Accounts.Where(acc => acc.Index == accountIndex).FirstOrDefault();
            else
                account = wallet.Accounts.First();

            return account.GetBalance();
        }

        public static Dictionary<IAccount, Money> AllAccountsBalances(Config config)
        {
            Load(config);

            var res = new Dictionary<IAccount, Money> ();

            foreach (var account in wallet.Accounts)
            {
                res[account] = account.GetBalance();
            }

            return res;
        }

        public static BitcoinAddress GetAddress(Config config, int accountIndex = 0)
        {
            network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemStorage(config.WalletId, network);

            if (!storage.Exists())
            {
                Console.WriteLine($"[GetAddress] Wallet {config.WalletId} doesn't exists.");

                return null;
            }

            wallet = storage.Load();

            IAccount account = wallet.Accounts[accountIndex];

            if (account is null) return null;

            var addr = account.GetReceiveAddress();

            wallet.Storage.Save();

            return addr;
        }

        public static BitcoinAddress[] GetAddresses(Config config, int accountIndex = 0, int addressAmount = 1)
        {
            network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemStorage(config.WalletId, network);

            if (!storage.Exists())
            {
                Console.WriteLine($"[GetAddress] Wallet {config.WalletId} doesn't exists.");

                return null;
            }

            wallet = storage.Load();

            IAccount account = wallet.Accounts[accountIndex];

            if (account is null) return null;

            var addrs = account.GetReceiveAddress(addressAmount);

            wallet.Storage.Save();

            return addrs;
        }

        public static void ReSync(Config config)
        {
            Load(config);

            wallet.SyncStarted += (s, e) =>
            {
                logger.Information("Sync started!");
            };

            wallet.SyncFinished += (s, e) =>
            {
                logger.Information("Sync finished!");
                logger.Information("TODO log txs found");
            };

            wallet.Resync();
            _ = PeriodicSave();

            WaitUntilEscapeIsPressed();
        }

        public static void Start(Config config, bool resync = false)
        {
            Load(config);

            wallet.SyncStarted += (s, e) =>
            {
                logger.Information("Sync started!");
            };

            wallet.SyncFinished += (s, e) =>
            {
                logger.Information("Sync finished, now waiting for txs!");

                wallet.Start();
            };

            if (resync) wallet.Resync();
            else wallet.Sync();

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
            Load(config);

            wallet.AddAccount(type, name, new { Wallet = wallet, WalletId = wallet.Id, Network = wallet.Network });

            wallet.Storage.Save();

            config.Network = wallet.Network.ToString();
            config.AddWallet(wallet.Id);

            config.SaveChanges();

            return wallet.Accounts.Last().ExtendedPubKey;
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
    }
}
