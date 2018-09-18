using System;
using NBitcoin;

namespace Liviano
{
    public class Wallet
    {
        public Wallet()
        {
            Console.WriteLine($"Key: {new Key().GetWif(Network.Main)}");
        }
    }
}
