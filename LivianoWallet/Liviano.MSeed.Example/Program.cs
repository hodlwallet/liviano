using System;
using System.Net.NetworkInformation;
using Liviano;

using NBitcoin;

namespace Liviano.MSeed.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            var mnemonic = args.Length > 0
                ? args[0]
                : "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

            var w = new Wallet();

            w.Init(mnemonic, "", network: Network.TestNet);

            w.Storage.Save();
        }
    }
}
