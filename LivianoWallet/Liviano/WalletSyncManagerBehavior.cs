using NBitcoin;/
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Liviano
{
    public class WalletSyncManagerBehavior : NodeBehavior
    {
        IWalletSyncManager _walletSyncManager;

        DateTimeOffset _SkipBefore;

        BlockLocator _CurrentPosition;
        private ConcurrentChain _Chain;
        private ConcurrentChain _ExplicitChain;

        long _FalsePositiveCount = 0;
        long _TotalReceived = 0;

        ConcurrentDictionary<uint256, uint256> _InFlight = new ConcurrentDictionary<uint256, uint256>();

        BoundedDictionary<uint256, MerkleBlock> _TransactionsToBlock = new BoundedDictionary<uint256, MerkleBlock>(1000);

        /// <summary>
        /// The maximum accepted false positive rate difference, the node will be disconnected if the actual false positive rate is higher than FalsePositiveRate + MaximumFalsePositiveRateDifference.
        /// </summary>
        public double MaximumFalsePositiveRateDifference { get; set; } = 0.1;


        public double ActualFalsePostiveRate
        {
            get
            {
                return (double)_FalsePositiveCount / (double)_TotalReceived;
            }
        }

        /// <summary>
        /// The expected false positive rate (between 1.0 and 0)
        /// </summary>
        public double FalsePositiveRate { get; set; }

        public WalletSyncManagerBehavior(IWalletSyncManager walletSyncManager, ConcurrentChain chain = null)
        {
            _walletSyncManager = walletSyncManager ?? throw new ArgumentNullException(nameof(walletSyncManager));
            FalsePositiveRate = 0.000005;
            _Chain = chain;
            _ExplicitChain = chain;
        }

        public override object Clone()
        {
            var clone = new WalletSyncManagerBehavior(_walletSyncManager, _ExplicitChain);
            clone.FalsePositiveRate = FalsePositiveRate;
            clone._SkipBefore = _SkipBefore;
            clone._CurrentPosition = _CurrentPosition;
            return clone;
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
            AttachedNode.StateChanged += ChangeOfAttachedNodeState;
            AttachedNode.MessageReceived += MessagedRecivedOnAttachedNode;

            
        }

        private void MessagedRecivedOnAttachedNode(NBitcoin.Protocol.Node node, NBitcoin.Protocol.IncomingMessage message)
        {
            var messagePayload = message.Message.Payload;

            if (messagePayload is MerkleBlockPayload)
                HandleMerkleBlockPayload(messagePayload as MerkleBlockPayload);
            else if (messagePayload is PongPayload)
                HandlePongPayload(messagePayload as PongPayload);
            else if (messagePayload is NotFoundPayload)
                HandleNotFoundPayLoad(messagePayload as NotFoundPayload);
            else if (messagePayload is InvPayload)
                HandleInvPayload(messagePayload as InvPayload, node);
            else if (messagePayload is TxPayload)
                HandleTxPayload(messagePayload as TxPayload);
            else
            {
                //Who cares!
            }
        }

        private void HandleTxPayload(TxPayload txPayload)
        {
            throw new NotImplementedException();
        }

        private void HandleInvPayload(InvPayload invPayload, Node node)
        {
            if (invPayload != null)
            {
                foreach (var inv in invPayload)
                {
                    if ((inv.Type & InventoryType.MSG_BLOCK) != 0)
                        node.SendMessageAsync(new GetDataPayload(new InventoryVector(InventoryType.MSG_FILTERED_BLOCK, inv.Hash)));
                    if ((inv.Type & InventoryType.MSG_TX) != 0)
                        node.SendMessageAsync(new GetDataPayload(inv));
                }
            }
        }

        private void HandleNotFoundPayLoad(NotFoundPayload notFoundPayload)
        {
            throw new NotImplementedException();
        }

        private void HandlePongPayload(PongPayload pongPayload)
        {
            throw new NotImplementedException();
        }

        private void HandleMerkleBlockPayload(MerkleBlockPayload merkleBlockPayload)
        {
            //if (merkleBlockPayload != null)
            //{
            //    if (!CheckFPRate(merkleBlockPayload))
            //    {
            //        return;
            //    }
            //    //merkleBlockPayload.Object.Header.
            //    foreach (var txId in merkleBlockPayload.Object.PartialMerkleTree.GetMatchedTransactions())
            //    {
            //        _TransactionsToBlock.AddOrUpdate(txId, merkleBlockPayload.Object, (k, v) => merkleBlockPayload.Object);
            //        var tx = _Tracker.GetKnownTransaction(txId);
            //        if (tx != null)
            //        {
            //            Notify(tx, merkleBlockPayload.Object);
            //        }
            //    }

            //    var h = merkleBlockPayload.Object.Header.GetHash();
            //    uint256 unused;
            //    if (_InFlight.TryRemove(h, out unused))
            //    {
            //        if (_InFlight.Count == 0)
            //        {
            //            UpdateCurrentProgress(h);
            //            StartScan(unused);
            //        }
            //    }
            //}
        }


        private bool CheckFPRate(MerkleBlockPayload merkleBlock)
        {
            var maxFPRate = FalsePositiveRate + MaximumFalsePositiveRateDifference;
            if (_TotalReceived > 100
                && ActualFalsePostiveRate >= maxFPRate)
            {
                var currentBlock = _Chain.GetBlock(merkleBlock.Object.Header.GetHash());
                if (currentBlock != null && currentBlock.Previous != null)
                    //UpdateCurrentProgress(currentBlock.Previous.HashBlock);
                this.AttachedNode.DisconnectAsync("The actual false positive rate exceed MaximumFalsePositiveRate");
                return false;
            }
            return true;
        }

        private void ChangeOfAttachedNodeState(NBitcoin.Protocol.Node node, NBitcoin.Protocol.NodeState oldState)
        {
            throw new NotImplementedException();
        }

        protected override void DetachCore()
        {
            throw new NotImplementedException();
        }
    }
}
