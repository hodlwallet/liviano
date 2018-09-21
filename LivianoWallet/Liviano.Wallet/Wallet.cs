using System;
using NBitcoin;

namespace Liviano
{
    public class Wallet
    {
        public Wallet()
        {
            Console.WriteLine("Nbitcoin stuff: " + new Key().GetBitcoinSecret(Network.Main).ToWif());
        }
    }
}
