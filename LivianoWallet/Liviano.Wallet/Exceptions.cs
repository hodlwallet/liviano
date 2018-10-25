using System;

namespace Liviano
{
    public class WalletException : Exception
    {
         public WalletException(string message) : base(message)
        {
        }
    }
}