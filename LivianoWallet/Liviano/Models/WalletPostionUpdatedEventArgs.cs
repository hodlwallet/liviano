using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano
{
    public class WalletPostionUpdatedEventArgs
    {
        public ChainedBlock PreviousPosition;
        public ChainedBlock NewPosition;
    }
}
