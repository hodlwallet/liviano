using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano
{
    public interface IWalletStorageProvider
    {
        Wallet LoadWallet();

        void SaveWallet(Wallet wallet);
    }
}
