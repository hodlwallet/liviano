using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano
{
    public interface IWalletStorageProvider
    {
        Wallet LoadWallet();

        bool WalletExists();

        void SaveWallet(Wallet wallet);
    }
}
