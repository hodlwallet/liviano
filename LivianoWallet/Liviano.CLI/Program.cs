using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommandLine;
using Serilog;
using Easy.MessageHub;

using Liviano;
using Liviano.Behaviors;
using Liviano.Managers;
using Liviano.Models;
using Liviano.Utilities;
using Liviano.Exceptions;
using NBitcoin;

namespace Liviano.CLI
{
    class Program
    {
        private static ILogger _Logger;

        static void Main(string[] args)
        {
            _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            Parser.Default.ParseArguments<MnemonicOptions, ExtendedKeyOptions, ExtendedPubKeyOptions, DeriveAddressOptions, AddressToScriptPubKeyOptions, NewWalletOptions, WalletBalanceOptions, NewAddressOptions, SendOptions, StartOptions>(args)
            .WithParsed<MnemonicOptions>(o => {
               string wordlist = "english";
               int wordCount = 24;

               if (o.WordCount != 0)
               {
                   wordCount = o.WordCount;
               }

               if (o.Wordlist != null)
               {
                   wordlist = o.Wordlist;
               }

               Console.WriteLine(WalletManager.NewMnemonic(wordlist, wordCount).ToString());
            })
            .WithParsed<ExtendedKeyOptions>(o => {
               string mnemonic = null;
               string passphrase = null;
               string network = "main";

               if (o.Mnemonic != null)
               {
                   mnemonic = o.Mnemonic;
               }
               else
               {
                   mnemonic = Console.ReadLine();
               }

               if (o.Passphrase != null)
               {
                   passphrase = o.Passphrase;
               }

               if (o.Testnet)
               {
                   network = "testnet";
               }
               else if (o.Regtest)
               {
                   network = "regtest";
               }

               var extKey = HdOperations.GetExtendedKey(mnemonic, passphrase);
               var wif = HdOperations.GetWif(extKey, network);

               Console.WriteLine(wif.ToString());
            })
            .WithParsed<ExtendedPubKeyOptions>(o => {
               string wif;
               string hdPath = "m/84'/0'/0'/0/0"; // Default BIP84 / Bitcoin / 1st account / receive / 1st pubkey
               string network = "main";

               if (o.Wif != null)
               {
                   wif = o.Wif;
               }
               else
               {
                   wif = Console.ReadLine();
               }

               if (o.Testnet)
               {
                   network = "testnet";
               }
               else if (o.Regtest)
               {
                   network = "regtest";
               }

               if (o.HdPath != null)
               {
                   hdPath = o.HdPath;
               }

               var extPubKey = HdOperations.GetExtendedPublicKey(wif, hdPath, network);
               var extPubKeyWif = HdOperations.GetWif(extPubKey, network);

               Console.WriteLine(extPubKeyWif.ToString());
            })
            .WithParsed<DeriveAddressOptions>(o => {
               string wif;
               int index = 1;
               bool isChange = false;
               string network = "main";
               string type = "p2wpkh";

               if (o.Wif != null)
               {
                   wif = o.Wif;
               }
               else
               {
                   wif = Console.ReadLine();
               }

               if (o.Index != null)
               {
                   index = (int) o.Index;
               }

               if (o.IsChange)
               {
                   isChange = o.IsChange;
               }

               if (o.Testnet)
               {
                   network = "testnet";
               }
               else if (o.Regtest)
               {
                   network = "regtest";
               }

               if (o.Type != null)
               {
                   type = o.Type;
               }

               Console.WriteLine(HdOperations.GetAddress(wif, index, isChange, network, type).ToString());
            })
            .WithParsed<AddressToScriptPubKeyOptions>(o => {
               string network = "main";
               string address = null;

               if (o.Testnet)
               {
                   network = "testnet";
               }
               else if (o.Regtest)
               {
                   network = "regtest";
               }

               if (o.Address != null)
               {
                   address = o.Address;
               }
               else
               {
                   address = Console.ReadLine();
               }

               Console.WriteLine(HdOperations.GetScriptPubKey(address, network).ToString());
            })
            .WithParsed<NewWalletOptions>(o => {
                string mnemonic = null;
                string name = Guid.NewGuid().ToString();
                string network = "main";

                if (o.Mnemonic != null)
                {
                    mnemonic = o.Mnemonic;
                }
                else
                {
                    mnemonic = Console.ReadLine();
                }

                if (o.Name != null)
                {
                    name = o.Name;
                }

                _Logger.Information("New wallet with name {name} to be created", name);

                if (o.Testnet)
                {
                    network = "testnet";
                }

                if (String.IsNullOrEmpty(mnemonic))
                {
                    _Logger.Error("Empty mnemonic");

                    throw new WalletException("Empty mnemonic");
                }

                // If the configuration exists, we just add a new wallet
                Config config;
                if (Config.Exists())
                {
                    config = Config.Load();

                    if (config.HasWallet(name))
                    {
                        _Logger.Error("Wallet already exists: {name}", name);

                        throw new WalletException($"Wallet already exists {name}");
                    }

                    config.WalletId = name;
                    config.Network = network;

                    config.Add(name);

                    config.SaveChanges();
                }
                else
                {
                    config = new Config(name, network);
                }

                config.SaveChanges();

                LightClient.CreateWallet(config, o.Password, mnemonic);

                Console.WriteLine(name);
            })
            .WithParsed<WalletBalanceOptions>(o => {
                string walletId = null;
                Config config = null;

                if (o.WalletId != null)
                {
                    walletId = o.WalletId;
                }

                if (Config.Exists())
                {
                    config = Config.Load();

                    if (walletId != null && !config.HasWallet(walletId))
                    {
                        _Logger.Error("Please create a new wallet for {walletId}", walletId);

                        throw new WalletException($"Please create a new wallet for {walletId}");
                    }

                    walletId = config.WalletId;
                }
                else
                {
                    _Logger.Error("Client configuration not found, use the command new-wallet to initalize your wallet with a mnemonic");

                    throw new WalletException("Please create a new wallet with the command new-wallet");
                }

                if (o.Testnet)
                {
                    config.Network = "testnet";
                }

                config.SaveChanges();

                bool shownBalance = false;

                try
                {
                    if (o.Name != null)
                    {
                        (string name, string hdPath, Money confirmedAmount, Money unconfirmedAmount) = LightClient.AccountBalance(config, o.Password, accountName: o.Name);

                        Console.WriteLine("Name, HdPath, Confirmed Amount, Unconfirmed Amount");
                        Console.WriteLine("==================================================");

                        Console.WriteLine($"{name}, {hdPath}, {confirmedAmount}, {unconfirmedAmount}");

                        shownBalance = true;
                    }

                    if (o.Index != null)
                    {
                        (string name, string hdPath, Money confirmedAmount, Money unconfirmedAmount) = LightClient.AccountBalance(config, o.Password, accountIndex: o.Index);

                        Console.WriteLine("Name, HdPath, Confirmed Amount, Unconfirmed Amount");
                        Console.WriteLine("==================================================");

                        Console.WriteLine($"{name}, {hdPath}, {confirmedAmount}, {unconfirmedAmount}");

                        shownBalance = true;
                    }
                }
                catch (WalletException e)
                {
                    _Logger.Error(e.ToString());

                    Console.WriteLine($"Account ({o.Name ?? o.Index}) not found.");
                }

                if (!shownBalance)
                {
                    var balances = LightClient.AllAccountsBalance(config, o.Password);

                    if (balances.Count() > 0)
                    {
                        Console.WriteLine("Name, HdPath, Confirmed Amount, Unconfirmed Amount");
                        Console.WriteLine("==================================================");

                        foreach (var balance in balances)
                        {
                            Console.WriteLine($"{balance.Name}, {balance.HdPath}, {balance.ConfirmedAmount}, {balance.UnConfirmedAmount}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No accounts with balances found");
                    }
                }
            })
            .WithParsed<NewAddressOptions>(o => {
                string walletId = null;
                Config config = null;

                if (o.WalletId != null)
                {
                    walletId = o.WalletId;
                }

                if (Config.Exists())
                {
                    config = Config.Load();

                    if (walletId != null && !config.HasWallet(walletId))
                    {
                        _Logger.Error("Please create a new wallet for {walletId}", walletId);

                        throw new WalletException($"Please create a new wallet for {walletId}");
                    }

                    walletId = config.WalletId;
                }
                else
                {
                    _Logger.Error("Client configuration not found, use the command new-wallet to initalize your wallet with a mnemonic");

                    throw new WalletException("Please create a new wallet with the command new-wallet");
                }

                if (o.Testnet)
                {
                    config.Network = "testnet";
                }

                config.SaveChanges();

                HdAddress address = null;

                try
                {
                    if (o.Name == null && o.Index == null)
                    {
                        address = LightClient.GetAddress(config, o.Password);
                    }
                    else if (o.Name != null)
                    {
                        address = LightClient.GetAddress(config, o.Password, accountName: o.Name);
                    }
                    else if (o.Index != null)
                    {
                        address = LightClient.GetAddress(config, o.Password, accountIndex: o.Index);
                    }
                }
                catch (InvalidOperationException e)
                {
                    _Logger.Error(e.ToString());

                    Console.WriteLine($"Unable to find account ({o.Name ?? o.Index})");

                    return;
                }

                if (o.Legacy)
                {
                    Console.WriteLine($"{address.LegacyAddress}");
                }
                else
                {
                    Console.WriteLine($"{address.Address}");
                }
            })
            .WithParsed<SendOptions>(async o => {
                string walletId = null;
                Config config = null;

                if (o.WalletId != null)
                {
                    walletId = o.WalletId;
                }

                if (Config.Exists())
                {
                    config = Config.Load();

                    if (walletId != null && !config.HasWallet(walletId))
                    {
                        _Logger.Error("Please create a new wallet for {walletId}", walletId);

                        throw new WalletException($"Please create a new wallet for {walletId}");
                    }

                    walletId = config.WalletId;
                }
                else
                {
                    _Logger.Error("Client configuration not found, use the command new-wallet to initalize your wallet with a mnemonic");

                    throw new WalletException("Please create a new wallet with the command new-wallet");
                }

                if (o.Testnet)
                {
                    config.Network = "testnet";
                }

                config.SaveChanges();

                bool wasCreated = false;
                bool wasSent = false;
                Transaction tx = null;
                string error = null;

                if (o.Name == null && o.Index == null)
                {
                    (wasCreated, wasSent, tx, error) = await LightClient.Send(config, o.Password, o.To, o.Amount, o.SatsPerByte);
                }
                else if (o.Name != null)
                {
                    (wasCreated, wasSent, tx, error) = await LightClient.Send(config, o.Password, o.To, o.Amount, o.SatsPerByte, accountName: o.Name);
                }
                else if (o.Index != null)
                {
                    (wasCreated, wasSent, tx, error) = await LightClient.Send(config, o.Password, o.To, o.Amount, o.SatsPerByte, accountIndex: o.Index);
                }

                Console.WriteLine($"TxId: {tx.GetHash()}");
                Console.WriteLine("=====================" + new string('=', tx.GetHash().ToString().Length));
                Console.WriteLine($"Size: {tx.GetVirtualSize()}");
                Console.WriteLine($"Created: {wasCreated}");
                Console.WriteLine($"Sent: {wasSent}");
                Console.WriteLine("Inputs");
                Console.WriteLine("------");

                foreach (var input in tx.Inputs)
                {
                    Console.WriteLine($"{input.PrevOut.Hash} ({input.PrevOut.N})");
                }

                Console.WriteLine($"Fees: {new Money((long) tx.GetVirtualSize() * o.SatsPerByte).ToDecimal(MoneyUnit.BTC)}");

                Console.WriteLine($"Hex: {tx.ToHex()}");
            })
            .WithParsed<StartOptions>(o => {
                string network = "main";
                string walletId = null;
                Config config = null;

                if (o.WalletId != null)
                {
                    walletId = o.WalletId;
                }

                if (Config.Exists())
                {
                    config = Config.Load();

                    if (walletId != null && !config.HasWallet(walletId))
                    {
                        _Logger.Error("Please create a new wallet for {walletId}", walletId);

                        throw new WalletException($"Please create a new wallet for {walletId}");
                    }

                    walletId = config.WalletId;
                }
                else
                {
                    _Logger.Error("Client configuration not found, use the command new-wallet to initalize your wallet with a mnemonic");

                    throw new WalletException("Please create a new wallet with the command new-wallet");
                }

                if (o.Testnet)
                {
                    network = "testnet";
                    config.Network = network;
                }

                if (o.NodesToConnect != 0)
                {
                    config.NodesToConnect = o.NodesToConnect;
                }
                else if (config.NodesToConnect == 0)
                {
                    config.NodesToConnect = 4; // safe default.
                }

                config.SaveChanges();

                LightClient.Start(config, o.Password, o.DateTime, o.DropTransactions);
            });
        }
    }
}
