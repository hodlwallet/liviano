using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano
{
    public interface IStorageProvider
    {
        Wallet LoadWallet();

        bool WalletExists();

        void SaveWallet(Wallet wallet);
    }
}
