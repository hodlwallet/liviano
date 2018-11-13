using NBitcoin;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.Text;

namespace Liviano
{
    public class WalletSyncManagerBehavior : NodeBehavior
    {
        WalletSyncManager _walletSyncManager;

        public double FalsePostiveRate { get; private set; }

        private ConcurrentChain _Chain;
        private ConcurrentChain _ExplicitChain;

        public WalletSyncManagerBehavior(WalletSyncManager walletSyncManager, ConcurrentChain chain = null)
        {
            _walletSyncManager = walletSyncManager ?? throw new ArgumentNullException(nameof(walletSyncManager));
            FalsePostiveRate = 0.000005;
            _Chain = chain;
            _ExplicitChain = chain;
        }

        public override object Clone()
        {
            throw new NotImplementedException();
        }

        protected override void AttachCore()
        {
            if (_Chain == null) //We need to insure we have a valid chain that is being synced constantly.
            {
                var chainBehavior = AttachedNode.Behaviors.Find<ChainBehavior>();
                if (chainBehavior == null)
                    throw new InvalidOperationException("A chain should either be passed in the constructor of TrackerBehavior, or a ChainBehavior should be attached on the node");
                _Chain = chainBehavior.Chain;
            }
            
        }

        protected override void DetachCore()
        {
            throw new NotImplementedException();
        }
    }
}
