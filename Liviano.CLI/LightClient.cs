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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NBitcoin;
using Serilog;

using Liviano.Exceptions;
using Liviano.Models;
using Liviano.Interfaces;
using Liviano.Bips;
using Liviano.Storages;
using Liviano.Extensions;
using Liviano.Utilities;

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
        public static Wallet NewWalletFromMnemonic(string mnemonic, string passphrase, Network network)
        {
            var wallet = new Wallet();

            wallet.Init(
                mnemonic: mnemonic,
                network: network,
                passphrase: passphrase,
                skipAuth: false
            );

            return wallet;
        }

        /// <summary>
        /// Creates new wallet from a wordliest, count and <see cref="Network"/>
        /// </summary>
        /// <param name="wordlist">The mnemonic separated by spaces</param>
        /// <param name="wordCount">The amount of words in the mnemonic</param>
        /// <param name="network">The <see cref="Network"/> it's from</param>
        public static Wallet NewWallet(string wordlist, int wordCount, string passphrase, Network network)
        {
            var mnemonic = Hd.NewMnemonic(wordlist, wordCount).ToString();

            return NewWalletFromMnemonic(mnemonic, passphrase, network);
        }

        /// <summary>
        /// Loads a wallet from a config
        /// </summary>
        /// <param name="config">a <see cref="Config"/> of the light wallet</param>
        /// <param name="passphrase">a <see cref="string"/> passphrase of the wallet</param>
        /// <param name="skipAuth">a <see cref="bool"/> to skip auth or not, useful for balance and getaddress</param>
        static void Load(Config config, string passphrase = null, bool skipAuth = false)
        {
            network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemWalletStorage(config.WalletId, network);

            if (!storage.Exists())
            {
                Console.WriteLine($"[Load] Wallet {config.WalletId} doesn't exists. Make sure you're on the right network");

                throw new WalletException("Invalid wallet id");
            }

            wallet = storage.Load(passphrase, out WalletException _, skipAuth);
        }

        public static async Task<(Transaction Transaction, string Error)> Send(
                Config config,
                string destinationAddress, double amount, decimal feeSatsPerByte,
                string passphrase = "")
        {
            Load(config, passphrase: passphrase, skipAuth: false);

            Transaction transaction = null;
            string error;

            try
            {
                (transaction, error) = wallet.CreateTransaction(wallet.CurrentAccount, destinationAddress, amount, feeSatsPerByte, true, passphrase);

                if (!string.IsNullOrEmpty(error)) return (transaction, error);
            }
            catch (Exception err)
            {
                Debug.WriteLine($"[Send] Failed to create transaction: {err.Message}");

                return (transaction, "Failed to create transaction");
            }

            try
            {
                bool res;
                (res, error) = await wallet.Broadcast(transaction);

                if (!res) return (transaction, $"Failed to broadcast transaction: {error}");
            }
            catch (Exception err)
            {
                Debug.WriteLine($"[Send] Failed to broadcast transaction: {err.Message}");

                return (transaction, $"Failed to broadcast transaction: {err.Message}");
            }

            // In the end is important to add the tx even though we don't know certain things
            // from it so we have the tx in our store already.
            var bitcoinAddress = BitcoinAddress.Create(destinationAddress, wallet.CurrentAccount.Network);

            var tx = new Tx
            {
                Id = transaction.GetHash(),
                Account = wallet.CurrentAccount,
                AccountId = wallet.CurrentAccount.Id,
                Network = network,
                Hex = transaction.ToHex(),
                IsRBF = transaction.RBF,
                CreatedAt = DateTimeOffset.UtcNow,
                AmountReceived = Money.Zero,
                TotalFees = transaction.GetFee(wallet.CurrentAccount.GetCoinsFromTransaction(transaction)),
                ScriptPubKey = bitcoinAddress.ScriptPubKey,
                IsReceive = false,
                IsSend = true,
                TotalAmount = transaction.TotalOut
            };
            tx.AmountSent = transaction.Outputs.Sum((@out) =>
            {
                var outAddr = @out.ScriptPubKey.GetDestinationAddress(network);

                if (!wallet.CurrentAccount.IsChange(outAddr))
                {
                    tx.SentScriptPubKey = @out.ScriptPubKey;
                    return @out.Value;
                }

                return Money.Zero;
            });

            tx.Account.AddTx(tx);

            foreach (var coin in transaction.Outputs.AsCoins())
            {
                var addr = coin.TxOut.ScriptPubKey.GetDestinationAddress(tx.Account.Network);

                if (tx.Account.IsChange(addr)) tx.Account.AddUtxo(coin);
            }

            wallet.Storage.Save();

            return (transaction, null);
        }

        /// <summary>
        /// Bump transaction's fee
        /// </summary>
        public static async Task<(Transaction Transaction, string Error)> BumpFee(
                Config config,
                string txId, decimal feeSatsPerByte,
                string passphrase = "")
        {
            Load(config, passphrase: passphrase, skipAuth: false);

            var account = wallet.CurrentAccount;
            var tx = account.Txs.FirstOrDefault((o) => string.Equals(o.Id.ToString(), txId));

            Guard.NotNull(tx, nameof(tx));

            Transaction bumpedTx = null;
            try
            {
                bumpedTx = account.BumpFee(tx, feeSatsPerByte);
            }
            catch (WalletException e)
            {
                Debug.WriteLine($"[BumpFee] Error: {e.Message}");

                return (bumpedTx, e.Message);
            }

            try
            {
                var (res, error) = await wallet.Broadcast(bumpedTx);

                if (!res) return (bumpedTx, $"Failed to broadcast transaction: {error}");
            }
            catch (Exception err)
            {
                Debug.WriteLine($"[Send] Failed to broadcast transaction: {err.Message}");

                return (bumpedTx, $"Failed to broadcast transaction: {err.Message}");
            }

            BitcoinAddress destinationAddress = null;
            foreach (var output in bumpedTx.Outputs)
            {
                var addr = output.ScriptPubKey.GetDestinationAddress(wallet.Network);
                if (
                    wallet.CurrentAccount.IsReceive(addr) || wallet.CurrentAccount.IsChange(addr)
                )
                    continue;

                destinationAddress = addr;
            }

            // In the end is important to add the tx even though we don't know certain things
            // from it so we have the tx in our store already.
            var bumpedTxModel = new Tx
            {
                Id = bumpedTx.GetHash(),
                Account = wallet.CurrentAccount,
                AccountId = wallet.CurrentAccount.Id,
                Network = network,
                Hex = bumpedTx.ToHex(),
                IsRBF = bumpedTx.RBF,
                CreatedAt = DateTimeOffset.UtcNow,
                AmountReceived = Money.Zero,
                TotalFees = bumpedTx.GetFee(wallet.CurrentAccount.GetCoinsFromTransaction(bumpedTx)),
                ScriptPubKey = destinationAddress.ScriptPubKey,
                IsReceive = false,
                IsSend = true,
                TotalAmount = bumpedTx.TotalOut
            };
            bumpedTxModel.AmountSent = bumpedTx.Outputs.Sum((@out) =>
            {
                var outAddr = @out.ScriptPubKey.GetDestinationAddress(network);

                if (!wallet.CurrentAccount.IsChange(outAddr))
                {
                    tx.SentScriptPubKey = @out.ScriptPubKey;
                    return @out.Value;
                }

                return Money.Zero;
            });

            wallet.CurrentAccount.AddTx(bumpedTxModel);
            foreach (var coin in bumpedTx.Outputs.AsCoins())
            {
                var addr = coin.TxOut.ScriptPubKey.GetDestinationAddress(wallet.CurrentAccount.Network);

                if (wallet.CurrentAccount.IsChange(addr)) wallet.CurrentAccount.AddUtxo(coin);
            }

            var originalTransaction = Transaction.Parse(tx.Hex, tx.Account.Network);
            foreach (var coin in originalTransaction.Outputs.AsCoins())
            {
                var addr = coin.TxOut.ScriptPubKey.GetDestinationAddress(tx.Account.Network);

                if (tx.Account.IsChange(addr) || tx.Account.IsReceive(addr)) tx.Account.RemoveUtxo(coin);
            }

            // Now we delete the old transaction
            wallet.CurrentAccount.RemoveTx(tx);

            wallet.Storage.Save();

            return (bumpedTx, null);
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
            Load(config, skipAuth: true);

            IAccount account;

            if (accountName != null)
                account = wallet.Accounts.FirstOrDefault(acc => acc.Name == accountName);
            else if (accountIndex != -1)
                account = wallet.Accounts.FirstOrDefault(acc => acc.Index == accountIndex);
            else
                account = wallet.Accounts.FirstOrDefault();

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
            Load(config, skipAuth: true);

            var res = new Dictionary<IAccount, Money>();

            foreach (var account in wallet.Accounts)
            {
                res[account] = account.GetBalance();
            }

            return res;
        }

        public static void AccountSummary(Config config, string accountName, int accountIndex)
        {
            Load(config, skipAuth: true);

            IAccount account;

            if (accountName != null)
                account = wallet.Accounts.FirstOrDefault(acc => acc.Name == accountName);
            else if (accountIndex != -1)
                account = wallet.Accounts.FirstOrDefault(acc => acc.Index == accountIndex);
            else
                account = wallet.Accounts.FirstOrDefault();

            var txs = new List<Tx> { };
            foreach (var tx in account.Txs)
                txs.Add(tx);

            if (txs.Count == 0) return;

            logger.Information("Account: Name: '{name}' Index: '{index}'", account.Name, account.Index);
            logger.Information("Balance: {balance} BTC", account.GetBalance());

            Console.WriteLine("");

            foreach (var tx in txs.OrderByDescending(o => o.CreatedAt))
            {
                logger.Information(
                    "Id: {txId} Amount: {txAmountSent}{txAmountReceived} Fees: {txFees} Height: {txBlockHeight} Confirmations: {txConfirmations} Time: {txCreatedAt}",
                    tx.Id, tx.AmountReceived > Money.Zero ? $"+{tx.AmountReceived}" : "", tx.AmountSent > Money.Zero ? $"-{tx.AmountSent}" : "", tx.TotalFees, tx.BlockHeight, tx.Confirmations, tx.CreatedAt
                );
            }

        }

        public static void AllAccountsSummaries(Config config)
        {
            Load(config, skipAuth: true);

            // Print transactions
            foreach (var account in wallet.Accounts)
            {
                AccountSummary(config, account.Name, account.Index);

                Console.WriteLine("");
            }
        }

        /// <summary>
        /// Gets an address from a <see cref="Config"/> and a index
        /// </summary>
        /// <param name="config">Light client config</param>
        /// <param name="accountIndex">An <see cref="int"/> of the account index</param>
        public static BitcoinAddress GetAddress(Config config, int accountIndex = 0)
        {
            Load(config, skipAuth: true);

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
            Load(config, skipAuth: true);

            IAccount account = wallet.Accounts[accountIndex];

            if (account is null) return null;

            var addrs = account.GetReceiveAddress(addressAmount, 0);

            wallet.Storage.Save();

            return addrs;
        }

        /// <summary>
        /// Syncs an account from current state to latest tx
        /// </summary>
        /// <param name="config">A <see cref="Config"/> for the client</param>
        public static void Sync(Config config)
        {
            Load(config, skipAuth: true);
            var startTime = DateTimeOffset.UtcNow;

            wallet.OnNewTransaction += (s, e) =>
            {
                logger.Information("Transaction found at height: {height}!", e.Tx.BlockHeight);
            };

            wallet.OnUpdateTransaction += (s, e) =>
            {
                logger.Information("Updated transaction at height: {height}!", e.Tx.BlockHeight);
            };

            wallet.OnSyncStarted += (s, e) =>
            {
                startTime = DateTimeOffset.UtcNow;
                logger.Information("Sync started at {time}!", DateTime.Now.ToString(@"yyyy/MM/dd hh:mm:ss tt", CultureInfo.InvariantCulture));
            };

            wallet.OnSyncFinished += (s, e) =>
            {
                logger.Information(
                    "Sync finished at {time}. Total sync time: {totalTime:n} seconds!",
                    DateTime.Now.ToString(@"yyyy/MM/dd hh:mm:ss tt", CultureInfo.InvariantCulture),
                    (DateTimeOffset.UtcNow - startTime).TotalSeconds
                );

                // Print transactions
                List<Tx> txs = new List<Tx> { };

                foreach (var tx in wallet.CurrentAccount.Txs.OrderByDescending(tx => tx.CreatedAt))
                    txs.Add(tx);

                if (txs.Count() == 0)
                {
                    logger.Information("No transactions found {sadFace}", ":(");

                    Quit();
                }
                else
                    logger.Information("Transactions:");

                if (txs.Count() != 0)
                {
                    foreach (var tx in txs)
                    {
                        logger.Information(
                            "Id: {txId} Amount: {txAmountSent}{txAmountReceived} Fees: {txFees} Height: {txBlockHeight} Confirmations: {txConfirmations} Time: {txCreatedAt}",
                            tx.Id, tx.AmountReceived > Money.Zero ? $"+{tx.AmountReceived}" : "", tx.AmountSent > Money.Zero ? $"-{tx.AmountSent}" : "", tx.TotalFees, tx.BlockHeight, tx.Confirmations, tx.CreatedAt
                        );
                    }

                    logger.Information("Total: {total}", wallet.CurrentAccount.GetBalance());
                }

                Quit();
            };

            wallet.OnNewHeaderNotified += (s, e) =>
            {
                logger.Information("New header found!");
            };

            wallet.Sync();
            _ = PeriodicSave();

            WaitUntilEscapeIsPressed();
        }

        /// <summary>
        /// Resyncs a wallet from a config
        /// </summary>
        /// <param name="config">A <see cref="Config"/> for the client</param>
        public static void ReSync(Config config)
        {
            Load(config, skipAuth: true);
            var startTime = DateTimeOffset.UtcNow;

            wallet.OnNewTransaction += (s, e) =>
            {
                logger.Information("Transaction found at height: {height}!", e.Tx.BlockHeight);
            };

            wallet.OnUpdateTransaction += (s, e) =>
            {
                logger.Information("Updated transaction at height: {height}!", e.Tx.BlockHeight);
            };

            wallet.OnSyncStarted += (s, e) =>
            {
                startTime = DateTimeOffset.UtcNow;
                logger.Information("Sync started at {time}!", DateTime.Now.ToString(@"yyyy/MM/dd hh:mm:ss tt", CultureInfo.InvariantCulture));
            };

            wallet.OnSyncFinished += (s, e) =>
            {
                logger.Information(
                    "Sync finished at {time}. Total sync time: {totalTime:n} seconds!",
                    DateTime.Now.ToString(@"yyyy/MM/dd hh:mm:ss tt", CultureInfo.InvariantCulture),
                    (DateTimeOffset.UtcNow - startTime).TotalSeconds
                );

                // Print transactions
                List<Tx> txs = new List<Tx> { };

                foreach (var tx in wallet.CurrentAccount.Txs.OrderByDescending(tx => tx.CreatedAt))
                    txs.Add(tx);

                if (txs.Count() == 0)
                {
                    logger.Information("No transactions found {sadFace}", ":(");

                    Quit();
                }
                else
                    logger.Information("Transactions:");

                if (txs.Count() != 0)
                {
                    foreach (var tx in txs)
                    {
                        logger.Information(
                            "Id: {txId} Amount: {txAmountSent}{txAmountReceived} Fees: {txFees} Height: {txBlockHeight} Confirmations: {txConfirmations} Time: {txCreatedAt}",
                            tx.Id, tx.AmountReceived > Money.Zero ? $"+{tx.AmountReceived}" : "", tx.AmountSent > Money.Zero ? $"-{tx.AmountSent}" : "", tx.TotalFees, tx.BlockHeight, tx.Confirmations, tx.CreatedAt
                        );
                    }

                    logger.Information("Total: {total}", wallet.CurrentAccount.GetBalance());
                }

                Quit();
            };

            wallet.OnNewHeaderNotified += (s, e) =>
            {
                logger.Information("New header found!");
            };

            wallet.Resync();
            _ = PeriodicSave();

            WaitUntilEscapeIsPressed();
        }

        public static void Ping(Config config)
        {
            Load(config, skipAuth: true);

            // Ping every 5 seconds
            wallet.ElectrumPool.PeriodicPing(
                o => logger.Information("Ping Successful at {time}!", o),
                o => logger.Information("Ping failed at {time}!", o),
                5000
            );

            wallet.ElectrumPool.ElectrumClient.OnConnected += (s, o) =>
            {
                logger.Information("Connected at {at}!", DateTimeOffset.UtcNow);
            };

            wallet.ElectrumPool.ElectrumClient.OnDisconnected += (s, o) =>
            {
                logger.Information("Disconnected at {at}!", DateTimeOffset.UtcNow);
            };

            _ = PeriodicSave();

            WaitUntilEscapeIsPressed();
        }

        public static void HeadersNotifications(Config config)
        {
            Load(config, skipAuth: true);

            wallet.ElectrumPool.PeriodicPing(
                o => logger.Information("Ping Successful at {time}!", o),
                o => logger.Information("Ping failed at {time}!", o),
                null
            );

            var cts = new CancellationTokenSource();
            wallet.ElectrumPool.SubscribeToHeaders(wallet, cts.Token);

            wallet.ElectrumPool.OnUpdatedHeader += (s, o) =>
            {
                logger.Information("New header found height: {height} time: {time}", o.Height, DateTimeOffset.UtcNow);
            };

            wallet.ElectrumPool.OnNewHeaderNotified += (s, o) =>
            {
                logger.Information("New header found height: {height} time: {time}", o.Height, DateTimeOffset.UtcNow);
            };

            _ = PeriodicSave();

            WaitUntilEscapeIsPressed();
        }

        /// <summary>
        /// Freeze Coin
        /// </summary>
        public static void FreezeCoin(Config config, string coinToFreeze)
        {
            if (wallet is null) Load(config, skipAuth: true);

            (string txId, int n) = ParseCoinString(coinToFreeze);

            if (!wallet.CurrentAccount.UnspentCoins.Exists((o) => o.Outpoint.Hash.ToString() == txId && o.Outpoint.N == n))
            {
                Console.WriteLine($"Could not find coin: {coinToFreeze}");

                return;
            }

            // Find our coin
            var coin = wallet.CurrentAccount.UnspentCoins.FirstOrDefault(o => o.Outpoint.Hash.ToString() == txId && o.Outpoint.N == n);

            wallet.CurrentAccount.FreezeUtxo(coin);
            wallet.Storage.Save();
        }

        /// <summary>
        /// Unfreeze Coin
        /// </summary>
        public static void UnfreezeCoin(Config config, string frozenCoin)
        {
            if (wallet is null) Load(config, skipAuth: true);

            (string txId, int n) = ParseCoinString(frozenCoin);

            // Find our coin
            var coin = wallet.CurrentAccount.FrozenCoins.FirstOrDefault(o => o.Outpoint.Hash.ToString() == txId && o.Outpoint.N == n);

            wallet.CurrentAccount.UnfreezeUtxo(coin);
            wallet.Storage.Save();
        }

        static (string, int) ParseCoinString(string coinStr)
        {
            var parsed = coinStr.Split(':');

            if (parsed.Length != 2)
                throw new WalletException("Invalid coin format, must be txid:N");

            var txId = parsed[0];
            var n = int.Parse(parsed[1]);

            return (txId, n);
        }

        /// <summary>
        /// Show Coin
        /// </summary>
        public static void ShowCoins(Config config)
        {
            if (wallet is null) Load(config, skipAuth: true);

            var acc = wallet.CurrentAccount;
            var total = Money.Zero; // Reusable for totals

            Console.WriteLine("Unspent Coins:");
            Console.WriteLine("==============\n");
            if (!acc.UnspentCoins.Any()) Console.WriteLine("-- Empty --");
            foreach (var unspentCoin in acc.UnspentCoins.ToList())
            {
                Console.WriteLine($"{unspentCoin.Outpoint.Hash}:{unspentCoin.Outpoint.N} Amount: {unspentCoin.Amount}");
                total += unspentCoin.Amount;
            }

            Console.WriteLine($"Total: {total}\n");

            Console.WriteLine("Spent Coins:");
            Console.WriteLine("============\n");
            total = Money.Zero;
            if (!acc.SpentCoins.Any()) Console.WriteLine("-- Empty --");
            foreach (var spentCoin in acc.SpentCoins.ToList())
            {
                Console.WriteLine($"{spentCoin.Outpoint.Hash}:{spentCoin.Outpoint.N} Amount: {spentCoin.Amount}");
                total += spentCoin.Amount;
            }
            Console.WriteLine($"Total: {total}\n");

            Console.WriteLine("Frozen Coins:");
            Console.WriteLine("=============\n");
            total = Money.Zero;
            if (!acc.FrozenCoins.Any()) Console.WriteLine("-- Empty --");
            foreach (var frozenCoin in acc.FrozenCoins.ToList())
            {
                Console.WriteLine($"{frozenCoin.Outpoint.Hash}:{frozenCoin.Outpoint.N} Amount: {frozenCoin.Amount}");
                total += frozenCoin.Amount;
            }
            Console.WriteLine($"Total: {total}");
        }

        /// <summary>
        /// Starts a wallet and waits for new txs
        /// </summary>
        /// <param name="config">A <see cref="Config"/> for the light client</param>
        /// <param name="resync">A <see cref="bool"/> asking to resync or continue syncing</param>
        public static void Start(Config config, bool resync = false)
        {
            Load(config, skipAuth: true);

            wallet.OnWatchStarted += (s, e) =>
            {
                logger.Information("Watch started!");
            };

            wallet.OnWatchAddressNotified += (s, e) =>
            {
                logger.Information(
                    "Got notification on account {0} from address: {1}, notification status hash: {2}",
                    e.Account.Index,
                    e.Address.ToString(),
                    e.Notification
                );
            };

            wallet.ElectrumPool.OnNewTransaction += (s, txArgs) =>
            {
                logger.Information($"Found new transaction!: {txArgs.Tx.Id} from address: {txArgs.Address}");
            };

            wallet.ElectrumPool.OnUpdateTransaction += (s, txArgs) =>
            {
                logger.Information($"Updated transaction!: {txArgs.Tx.Id} from address: {txArgs.Address}");
            };

            wallet.OnNewHeaderNotified += (s, headerArgs) =>
            {
                logger.Information($"New header notified!, height: {headerArgs.Height}");
            };

            wallet.OnSyncStarted += (s, e) =>
            {
                logger.Information("Sync started!");
            };

            wallet.OnSyncFinished += (s, e) =>
            {
                logger.Information("Sync finished!");

                // Print transactions
                List<Tx> txs = new List<Tx> { };

                foreach (var tx in wallet.CurrentAccount.Txs.OrderByDescending(tx => tx.CreatedAt))
                    txs.Add(tx);

                if (txs.Count() > 0)
                {
                    logger.Information("Transactions:");

                    foreach (var tx in txs)
                    {
                        logger.Information($"Id: {tx.Id} Amount: {(tx.IsReceive ? tx.AmountReceived : tx.AmountSent)} Fees: {tx.TotalFees}");
                    }

                    logger.Information($"Total: {wallet.CurrentAccount.GetBalance()}");
                }

                wallet.Watch();
            };

            if (!resync)
            {
                wallet.Watch();

                _ = PeriodicSave();

                WaitUntilEscapeIsPressed();

                return;
            }

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
            Load(config, skipAuth: true);

            var network = wallet.Network;
            var id = wallet.Id;

            wallet.AddAccount(type, name, new { Wallet = wallet, WalletId = id, Network = network });

            wallet.Storage.Save();

            config.Network = wallet.Network.ToString();
            config.AddWallet(wallet.Id);

            config.SaveChanges();

            return wallet.Accounts.Last().ExtPubKey.ToString();
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
                    logger.Information("Stop will be handled by IDE");

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
                    logger.Information("Closing thread with pid: {pid}", thread.Id);
            }

            logger.Information("Closing process with pid: {pid}", process.Id);
            logger.Information("bye!");

            process.Close();

            Environment.Exit(retVal);
        }
    }
}
