using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano
{
    public class WalletSyncManagerBehavior : NodeBehavior
    {
        WalletSyncManager _walletSyncManager;

        public WalletSyncManagerBehavior(WalletSyncManager walletSyncManager)
        {
            _walletSyncManager = walletSyncManager;
        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }

        protected override void AttachCore()
        {
            throw new NotImplementedException();
        }

        protected override void DetachCore()
        {
            throw new NotImplementedException();
        }
    }
}
