using Liviano.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano.Interfaces
{
    public interface IStorageProvider
    {
        Wallet LoadWallet();

        bool WalletExists();

        void SaveWallet(Wallet wallet);
    }
}
