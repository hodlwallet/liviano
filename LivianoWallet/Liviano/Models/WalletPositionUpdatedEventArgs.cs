using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano.Models
{
    public class WalletPositionUpdatedEventArgs
    {
        public ChainedBlock PreviousPosition;
        public ChainedBlock NewPosition;
    }
}
