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

namespace Liviano.CLI
{
    class Program
    {
        private static ILogger _Logger;

        static void Main(string[] args)
        {
            _Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            Parser.Default.ParseArguments<NewMnemonicOptions, GetExtendedKeyOptions, GetExtendedPubKeyOptions, DeriveAddressOptions, AddressToScriptPubKeyOptions, NewWalletOptions, StartOptions>(args)
            .WithParsed<NewMnemonicOptions>(o => {
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
            .WithParsed<GetExtendedKeyOptions>(o => {
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
            .WithParsed<GetExtendedPubKeyOptions>(o => {
               string wif;
               string hdPath = "m/44'/0'/0'/0/0"; // Default BIP44 / Bitcoin / 1st account / receive / 1st pubkey
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

                    if (!config.HasWallet(walletId))
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

                    config.SaveChanges();
                }

                LightClient.Start(config, o.Password);
            });
        }
    }
}
