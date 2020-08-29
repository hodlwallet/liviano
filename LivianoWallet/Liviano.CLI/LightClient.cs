//
// LightClient.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
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
using System.Linq;
using System.Threading.Tasks;

using NBitcoin;
using Serilog;

using Liviano.Exceptions;
using Liviano.Models;
using Liviano.Interfaces;
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

        /// <summary>
        /// Saves the wallet every <see cref="PERIODIC_SAVE_DELAY"/>
        /// </summary>
        static async Task PeriodicSave()
        {
            while (true)
            {
                await Save();

                await Task.Delay(PERIODIC_SAVE_DELAY);
            }
        }

        /// <summary>
        /// Saves the wallet
        /// </summary>
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

        /// <summary>
        /// Creates a new wallet from a mnemonic <see cref="string"/> on a <see cref="Network"/>
        /// </summary>
        public static Wallet NewWalletFromMnemonic(string mnemonic, Network network)
        {
            var wallet = new Wallet();

            wallet.Init(mnemonic: mnemonic, network: network);

            return wallet;
        }

        /// <summary>
        /// Creates new wallet from a wordliest, count and <see cref="Network"/>
        /// </summary>
        /// <param name="wordlist">The mnemonic separated by spaces</param>
        /// <param name="wordCount">The amount of words in the mnemonic</param>
        /// <param name="network">The <see cref="Network"/> it's from</param>
        public static Wallet NewWallet(string wordlist, int wordCount, Network network)
        {
            var mnemonic = Hd.NewMnemonic(wordlist, wordCount).ToString();

            return NewWalletFromMnemonic(mnemonic, network);
        }

        /// <summary>
        /// Loads a wallet from a config
        /// </summary>
        /// <param name="config">a <see cref="Config"/> of the light wallet</param>
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

        public static async Task<(Transaction Tx, string Error)> Send(
                Config config,
                string destinationAddress, double amount, int feeSatsPerByte,
                IAccount account, string password = "")
        {
            Transaction tx = null;
            string error = null;

            try
            {
                (tx, error) = wallet.CreateTransaction(account, destinationAddress, amount, feeSatsPerByte, password);

                if (!string.IsNullOrEmpty(error))
                {
                    return (tx, error);
                }
            }
            catch (Exception err)
            {
                Debug.WriteLine($"[Send] Failed to create transcation: {err.Message}");

                return (tx, "Failed to create transaction");
            }

            try
            {
                var res = await wallet.BroadcastTransaction(tx);

                if (!res) return (tx, "Failed to broadcast transaction");
            }
            catch (Exception err)
            {
                Debug.WriteLine($"[Send] Failed to broadcast transcation: {err.Message}");

                return (tx, "Failed to broadcast transaction");
            }

            return (tx, null);
        }

        /// <summary>
        /// Gets accounts balance
        /// </summary>
        /// <returns>A <see cref="Money"/> with the balance in Bitcoin</returns>
        /// <param name="config">Light client config</param>
        /// <param name="accountName">Account name to check balance for<param>
        /// <param name="accountIndex">Index of the account, 0 for first</param>
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

            try
            {
                return account.GetBalance();
            }
            catch (NullReferenceException e)
            {
                Debug.WriteLine($"[AccountBalance] Error {e.Message}");

                return 0L;
            }
        }

        /// <summary>
        /// Gets a dictionary of key <see cref="IAccount"/> and a <see cref="Money"/> as value from the <see cref="IWallet"/>
        /// </summary>
        /// <param name="config">Config of the light client<param>
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

        /// <summary>
        /// Gets an address from a <see cref="Config"/> and a index
        /// </summary>
        /// <param name="config">Light client config</param>
        /// <param name="accountIndex">An <see cref="int"/> of the account index</param>
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

        /// <summary>
        /// Gets a number of addresses in an array
        /// </summary>
        /// <param name="config">A <see cref="Config"/></param>
        /// <param name="addressAmount">Amount of addresses to generate</param>
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

        /// <summary>
        /// Resyncs a wallet from a config
        /// </summary>
        /// <param name="config">A <see cref="Config"/> for the client</param>
        public static void ReSync(Config config)
        {
            Load(config);

            wallet.OnSyncStarted += (s, e) =>
            {
                logger.Information("Sync started!");
            };

            wallet.OnSyncFinished += (s, e) =>
            {
                logger.Information("Sync finished!");

                // Print transactions
                List<Tx> txs = new List<Tx> { };

                foreach (var account in wallet.Accounts)
                    foreach (var tx in account.Txs)
                        txs.Add(tx);

                if (txs.Count() == 0)
                    Quit();
                else
                    logger.Information("Transactions:");

                foreach (var tx in txs)
                    logger.Information(
                        $"Id: {tx.Id} Amount: {(tx.IsReceive ? tx.AmountReceived : tx.AmountSent)}"
                    );

                Quit();
            };

            wallet.Resync();
            _ = PeriodicSave();

            WaitUntilEscapeIsPressed();
        }

        /// <summary>
        /// Starts a wallet and waits for new txs
        /// </summary>
        /// <param name="config">A <see cref="Config"/> for the light client</param>
        /// <param name="resync">A <see cref="bool"/> asking to resync or continue syncing</param>
        public static void Start(Config config, bool resync = false)
        {
            Load(config);

            wallet.OnSyncStarted += (s, e) =>
            {
                logger.Information("Sync started!");
            };

            wallet.OnSyncFinished += (s, e) =>
            {
                logger.Information("Sync finished!");

                // Print transactions
                List<Tx> txs = new List<Tx> { };

                foreach (var account in wallet.Accounts)
                    foreach (var tx in account.Txs)
                        txs.Add(tx);

                if (txs.Count() > 0)
                {
                    logger.Information("Transactions:");

                    foreach (var tx in txs)
                        logger.Information($"Id: {tx.Id} Amount: {(tx.IsReceive ? tx.AmountReceived : tx.AmountSent)}");
                }

                wallet.Watch();
            };

            wallet.OnWatchStarted += (s, e) =>
            {
                logger.Information("Watch started!");
            };

            wallet.Sync();

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

        /// <summary>
        /// Waits until the user press esc
        /// </summary>
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
                Quit();
            }
        }

        /// <summary>
        /// Quits the program
        /// </summary>
        /// <param name="retVal">Unix return value an <see cref="int"/></param>
        static void Quit(int retVal = 0)
        {
            logger.Information("Saving...");
            Save().Wait();

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

            process.Close();

            Environment.Exit(retVal);
        }
    }
}
