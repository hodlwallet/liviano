using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using System.Collections.Concurrent;
using System.Linq;
using Liviano.Utilities;
using Easy.MessageHub;
using Liviano.Managers;
using Liviano.Models;
using Liviano.Enums;
using Liviano.Interfaces;
using Liviano.Behaviors;

namespace Liviano.Managers
{
    public class BroadcastManager : IBroadcastManager
    {

        NodesCollection _Nodes;
        MessageHub _EventHub;
        public BroadcastManager(NodesGroup nodeGroup)
        {
            _Nodes = nodeGroup.ConnectedNodes;
            this.Broadcasts = new ConcurrentDictionary<TransactionBroadcastEntry, object>(2, 0);

            _EventHub = MessageHub.Instance;

        }
        //Concurrent dictionary ignoring the value
        public ConcurrentDictionary<TransactionBroadcastEntry,object> Broadcasts { get; }

        public EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        public void AddOrUpdate(Transaction transaction, TransactionState state, string errorMessage = "")
        {
            TransactionBroadcastEntry broadcastEntry = this.Broadcasts.Keys.FirstOrDefault(x => x.Transaction.GetHash() == transaction.GetHash());

            if (broadcastEntry == null)
            {
                broadcastEntry = new TransactionBroadcastEntry(transaction, state, errorMessage);
                this.Broadcasts.TryAdd(broadcastEntry,new object());
            }
            else if (broadcastEntry.State != state)
            {
                broadcastEntry.State = state;
            }

            TransactionStateChanged?.Invoke(this, broadcastEntry);
            //_EventHub.Publish(broadcastEntry);

        }

        public async Task BroadcastTransactionAsync(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            if (this.IsPropagated(transaction))
                return;


            await this.PropagateTransactionToPeersAsync(transaction, this._Nodes).ConfigureAwait(false);
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
            var txPayload = new TxPayload(transaction);

            foreach (var node in nodes)
            {
                    await node.SendMessageAsync(invPayload).ConfigureAwait(false);
                    await node.SendMessageAsync(txPayload).ConfigureAwait(false);
            }
        }

        protected bool IsPropagated(Transaction transaction)
        {
            TransactionBroadcastEntry broadcastEntry = this.GetTransaction(transaction.GetHash());
            return (broadcastEntry != null) && (broadcastEntry.State == TransactionState.Propagated);
        }
    }
}
