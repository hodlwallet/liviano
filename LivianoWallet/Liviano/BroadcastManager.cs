using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using System.Collections.Concurrent;
using System.Linq;
using Liviano.Utilities;

namespace Liviano
{
    class BroadcastManager : IBroadcastManager
    {

        NodesCollection nodes;

        public BroadcastManager(NodesGroup nodeGroup)
        {
            nodes = nodeGroup.ConnectedNodes;

            this.Broadcasts = new ConcurrentDictionary<TransactionBroadcastEntry, object>(2, 0);
        }

        //Concurrent dictionary ignoring the value
        public ConcurrentDictionary<TransactionBroadcastEntry,object> Broadcasts { get; }

        public void AddOrUpdate(Transaction transaction, TransactionState state, string errorMessage = "")
        {
            TransactionBroadcastEntry broadcastEntry = this.Broadcasts.Keys.FirstOrDefault(x => x.Transaction.GetHash() == transaction.GetHash());

            if (broadcastEntry == null)
            {
                broadcastEntry = new TransactionBroadcastEntry(transaction, state, errorMessage);
                this.Broadcasts.TryAdd(broadcastEntry,new object());

                //TODO : Add eventhub handler for publishing event
                //this.OnTransactionStateChanged(broadcastEntry);
            }
            else if (broadcastEntry.State != state)
            {
                broadcastEntry.State = state;

                //TODO : Add eventhub handler for publishing event
                //this.OnTransactionStateChanged(broadcastEntry);
            }
        }

        public async Task BroadcastTransactionAsync(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            if (this.IsPropagated(transaction))
                return;


            await this.PropagateTransactionToPeersAsync(transaction, this.nodes).ConfigureAwait(false);
        }

        public TransactionBroadcastEntry GetTransaction(uint256 transactionHash)
        {
            TransactionBroadcastEntry txEntry = this.Broadcasts.Keys.FirstOrDefault(x => x.Transaction.GetHash() == transactionHash);
            return txEntry ?? null;
        }


        /// <summary>
        /// Sends transaction to peers.
        /// </summary>
        /// <param name="transaction">Transaction that will be propagated.</param>
        /// <param name="peers">Peers to whom we will propagate the transaction.</param>
        protected async Task PropagateTransactionToPeersAsync(Transaction transaction, NodesCollection nodes)
        {
            this.AddOrUpdate(transaction, TransactionState.ToBroadcast);

            var invPayload = new InvPayload(transaction);

            foreach (var node in nodes)
            {
                try
                {
                    await node.SendMessageAsync(invPayload).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //Error handling
                }
            }
        }


        protected bool IsPropagated(Transaction transaction)
        {
            TransactionBroadcastEntry broadcastEntry = this.GetTransaction(transaction.GetHash());
            return (broadcastEntry != null) && (broadcastEntry.State == TransactionState.Propagated);
        }
    }
}
