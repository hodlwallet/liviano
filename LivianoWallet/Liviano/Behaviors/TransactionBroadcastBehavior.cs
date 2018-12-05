using Liviano.Enums;
using Liviano.Interfaces;
using Liviano.Managers;
using Liviano.Models;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liviano.Behaviors
{
    public class TransactionBroadcastBehavior : NodeBehavior
    {

        IBroadcastManager _BroadcastManager;


        volatile PingPayload _PreviousPing;


        public bool HasPonged { get; private set; }

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
            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
            //AttachedNode.SendMessageAsync(new MempoolPayload());
           // SendPing();
        }

        private void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            if (node.State != NodeState.HandShaked)
            {
                //SendPing();
            }
        }

        protected override void DetachCore()
        {
            this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;
        }

        private void SendPing()
        {
            var node = AttachedNode;
            if (node != null) //Insure we have a node
            {
                _PreviousPing = null; //Set to null

#pragma warning disable 612, 618
                HasPonged = false; // Set state to unPonged
#pragma warning disable 612, 618

              
                var ping = new PingPayload() // Create a ping payload
                {
                    Nonce = RandomUtils.GetUInt64() // Add a random noonce, we will expect this in the future
                };
                _PreviousPing = ping; //The last ping will the one we just created
                node.SendMessageAsync(ping); // Now send it
            }
        }


        private void HandlePongPayload(PongPayload pongPayload)
        {
            if (pongPayload != null) //Make sure pong is valid
            {
                var ping = _PreviousPing;
                if (ping != null && pongPayload.Nonce == ping.Nonce) // If the pong matches our previous Pings noonce
                {
                    _PreviousPing = null;
                    HasPonged = true; //We can assume they recieved our filter and its loaded
                }
            }
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

                case PongPayload pongPayload:
                    this.HandlePongPayload(pongPayload);
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
