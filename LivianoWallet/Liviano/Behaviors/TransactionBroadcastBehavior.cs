using Liviano.Enums;
using Liviano.Interfaces;
using Liviano.Managers;
using Liviano.Models;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liviano.Behaviors
{
    class TransactionBroadcastBehavior : NodeBehavior
    {

        IBroadcastManager _BroadcastManager;

        public TransactionBroadcastBehavior(IBroadcastManager broadcastManager)
        {
            _BroadcastManager = broadcastManager;
        }

        public override object Clone()
        {
            return new TransactionBroadcastBehavior(_BroadcastManager);
        }

        protected override void AttachCore()
        {
            this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
        }


        protected override void DetachCore()
        {
            this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
        }



        private async void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case GetDataPayload getDataPayload:
                    await this.ProcessGetDataPayloadAsync(node, getDataPayload).ConfigureAwait(false);
                    break;

                case InvPayload invPayload:
                    this.ProcessInvPayload(invPayload);
                    break;
            }
        }

        private void ProcessInvPayload(InvPayload invPayload)
        {
            // if node has transaction we broadcast
            foreach (InventoryVector inv in invPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
            {
                TransactionBroadcastEntry txEntry = _BroadcastManager.GetTransaction(inv.Hash);
                if (txEntry != null)
                {
                    _BroadcastManager.AddOrUpdate(txEntry.Transaction, TransactionState.Propagated);
                }
            }
        }

        private async Task ProcessGetDataPayloadAsync(Node node, GetDataPayload getDataPayload)
        {
            // If node asks for transaction we want to broadcast.
            foreach (InventoryVector inv in getDataPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
            {
                TransactionBroadcastEntry txEntry = _BroadcastManager.GetTransaction(inv.Hash);
                if ((txEntry != null) && (txEntry.State != TransactionState.CantBroadcast))
                {
                    await node.SendMessageAsync(new TxPayload(txEntry.Transaction)).ConfigureAwait(false);
                    if (txEntry.State == TransactionState.ToBroadcast)
                    {
                        _BroadcastManager.AddOrUpdate(txEntry.Transaction, TransactionState.Broadcasted);
                    }
                }
            }
        }

    }
}
