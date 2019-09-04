using System;
using System.IO;

using NBitcoin;

using Newtonsoft.Json;

using Liviano.Extensions;

namespace Liviano.MSeed.Example
{
    interface IPerson
    {
        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }
    }

    class Person : IPerson
    {
        public string Name { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to a demo");

            Console.WriteLine("Delete previous wallets");

            Directory.Delete("wallets", recursive: true);

            //Console.WriteLine(contents);
            var mnemonic = args.Length > 0
                ? args[0]
                : "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

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
                Console.WriteLine($"{addr.ToString()}, scriptHash: {addr.ToScriptHash().ToHex()}");
            }
            else
            {
                int n = account.GapLimit;
                Console.WriteLine($"Addresses ({n})");

                foreach (var addr in account.GetReceiveAddress(n))
                {
                    Console.WriteLine($"{addr.ToString()}, scriptHash: {addr.ToScriptHash().ToHex()}");
                }

                account.ExternalAddressesCount = 0;
            }

            Console.WriteLine("\nPress [ESC] to stop!\n");

            Console.WriteLine("Syncing...");

            _ = w.Sync();

            while (true)
            {
                var input = Console.ReadKey();

                if (input.Key == ConsoleKey.Escape)
                    break;
            }
        }
    }
}
