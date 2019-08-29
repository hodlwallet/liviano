using System;
using System.Net.NetworkInformation;
using Liviano;

using NBitcoin;
using Newtonsoft.Json;

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
            //var w = new Wallet();
            //w.Init("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about");

            //var contents = JsonConvert.SerializeObject(w);

            //Console.WriteLine(contents);
            var mnemonic = args.Length > 0
                ? args[0]
                : "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

            var w = new Wallet();

            w.Init(mnemonic, "", network: Network.TestNet);

            w.AddAccount("bip141");

            var contents = JsonConvert.SerializeObject(w);

            Console.WriteLine(contents);

            w.Storage.Save();
        }
    }
}
