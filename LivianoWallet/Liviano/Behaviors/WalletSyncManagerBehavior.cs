using Easy.MessageHub;
using Liviano.Enums;
using Liviano.Interfaces;
using Liviano.Managers;
using Liviano.Models;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.SPV;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Liviano.Behaviors
{
    public class WalletSyncManagerBehavior : NodeBehavior
    {
        IWalletSyncManager _walletSyncManager;

        DateTimeOffset _SkipBefore { get { return _walletSyncManager.DateToStartScanningFrom; } set { _walletSyncManager.DateToStartScanningFrom = value; } }

        private BlockLocator _CurrentPosition { get { return _walletSyncManager.CurrentPosition; } set { _walletSyncManager.CurrentPosition = value; } }

        private ConcurrentChain _Chain;

        private ConcurrentChain _ExplicitChain;

        long _FalsePositiveCount = 0;

        long _TotalReceived = 0;

        ConcurrentDictionary<uint256, uint256> _InFlight = new ConcurrentDictionary<uint256, uint256>();

        BoundedDictionary<uint256, MerkleBlock> _TransactionsToBlock = new BoundedDictionary<uint256, MerkleBlock>(1000);
        object locker = new object();

        volatile PingPayload _PreviousPing;

// Suppresses warnings for obsolete use of SPV code
#pragma warning disable 612, 618
        public FilterState _FilterState { get; private set; }
#pragma warning restore 612, 618

        private ConcurrentBag<Action> _ActionsToFireWhenFilterIsLoaded;

        private ScriptTypes _ScriptType;

        private ILogger _Logger;

        private MessageHub _messageHub;

        private object _CurrentPositionlocker = new object();

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

        public WalletSyncManagerBehavior(ILogger logger, IWalletSyncManager walletSyncManager, ScriptTypes scriptType = ScriptTypes.SegwitAndLegacy, ConcurrentChain chain = null)
        {
            _Logger = logger;

            _walletSyncManager = walletSyncManager ?? throw new ArgumentNullException(nameof(walletSyncManager));
            FalsePositiveRate = 0.000005;
            _Chain = chain;
            _ExplicitChain = chain;
            _ScriptType = scriptType;
            _ActionsToFireWhenFilterIsLoaded = new ConcurrentBag<Action>();

            _messageHub = MessageHub.Instance;
        }

        public override object Clone()
        {
            var clone = new WalletSyncManagerBehavior(_Logger, _walletSyncManager, _ScriptType, _ExplicitChain);

            clone.FalsePositiveRate = FalsePositiveRate;
            clone._SkipBefore = _SkipBefore;
            clone._CurrentPosition = _CurrentPosition;

            return clone;
        }

        public void RefreshBloomFilter()
        {
            SetBloomFilter();
        }

        private void SetBloomFilter()
        {
            var node = AttachedNode;
            if (node != null) //Insure we have a node
            {
                _PreviousPing = null; //Set to null
                var filter = _walletSyncManager.CreateBloomFilter(FalsePositiveRate,_ScriptType); //Create bloom filter

#pragma warning disable 612, 618
                _FilterState = FilterState.Unloaded; // Set state to unloaded as we are attempting to load
#pragma warning disable 612, 618

                node.SendMessageAsync(new FilterLoadPayload(filter)); // Send that shit, load filter
                _FilterState = FilterState.Loading; // Set state to loading
                var ping = new PingPayload() // Create a ping payload
                {
                    Nonce = RandomUtils.GetUInt64() // Add a random noonce, we will expect this in the future
                };
                _PreviousPing = ping; //The last ping will the one we just created
                node.SendMessageAsync(ping); // Now send it
            }
        }

        protected override void AttachCore()
        {
            _Logger.Information(
                "Connected to: {host}:{port} ({version})",
                AttachedNode.RemoteSocketAddress.ToString(),
                AttachedNode.RemoteSocketPort,
                AttachedNode.Version
            );

            AttachedNode.StateChanged += ChangeOfAttachedNodeState;
            AttachedNode.MessageReceived += MessagedRecivedOnAttachedNode;
            if (_Chain == null) // We need to insure we have a valid chain that is being synced constantly.
            {
                var chainBehavior = AttachedNode.Behaviors.Find<PartialChainBehavior>();
                if (chainBehavior == null)
                    throw new InvalidOperationException("A chain should either be passed in the constructor of TrackerBehavior, or a ChainBehavior should be attached on the node");
                _Chain = chainBehavior.Chain;
            }


            Timer timer = new Timer(StartScan, null, 5000, 10000);
            RegisterDisposable(timer);


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
            }
        }

        private void StartScan(object p)
        {
            var node = AttachedNode;
            if (_FilterState != FilterState.Loaded)
            {
                _ActionsToFireWhenFilterIsLoaded.Add(() => StartScan(p));
                return;
            }
            if (!IsScanning(node))
            {
                if (Monitor.TryEnter(locker))
                {
                    try
                    {
                        if (!IsScanning(node))
                        {

                            GetDataPayload payload = new GetDataPayload();
                            var positionInChain = _Chain.FindFork(_CurrentPosition);

                            if (positionInChain == null)
                            {
                                _Logger.Warning("Chain synced to height {height}", _Chain.Tip.Height);
                                _Logger.Warning("Position to start scanning from is not in the chain we are trying to sync, hmmm...");
                                return;
                            }

                            var blocks = _Chain
                                .EnumerateAfter(positionInChain)
                                .Where(b => b.Header.BlockTime + TimeSpan.FromHours(5.0) > _SkipBefore) //Take 5 more hours, block time might not be right
                                .Partition(100).FirstOrDefault() ?? new List<ChainedBlock>();

                            foreach (var block in blocks)
                            {
                                payload.Inventory.Add(new InventoryVector(InventoryType.MSG_FILTERED_BLOCK, block.HashBlock));
                                _InFlight.TryAdd(block.HashBlock, block.HashBlock);
                            }
                            if (payload.Inventory.Count > 0)
                                node.SendMessageAsync(payload);
                        }
                    }
                    finally
                    {
                        Monitor.Exit(locker);
                    }
                }
            }
        }

        private bool IsScanning(Node node)
        {
            return _InFlight.Count != 0 || _CurrentPosition == null || node == null;
        }

        private void HandlePongPayload(PongPayload pongPayload)
        {
            if (pongPayload != null) // Make sure pong is valid
            {
                var ping = _PreviousPing;
                if (ping != null && pongPayload.Nonce == ping.Nonce) // If the pong matches our previous Pings noonce
                {
                    _PreviousPing = null;
                    _FilterState = FilterState.Loaded; // We can assume they recieved our filter and its loaded
                    foreach (var item in _ActionsToFireWhenFilterIsLoaded) // Iterate over items we had to dock
                    {
                        item(); // Execute them
                    }
                    _ActionsToFireWhenFilterIsLoaded = new ConcurrentBag<Action>(); // Refresh collection
                }
            }
        }

        private void HandleTxPayload(TxPayload txPayload)
        {
            if (txPayload != null)
            {
                var tx = txPayload.Object;
                MerkleBlock blk;
                var h = tx.GetHash();
                _TransactionsToBlock.TryGetValue(h, out blk);
                if (blk != null)
                {
                    _Logger.Information("Found a transaction bounded to a block");
                }
                NotifyWalletSyncManager(tx, blk);
            }
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
            if (notFoundPayload != null)
            {
                foreach (var txid in notFoundPayload) // These payloads cant be found
                {
                    uint256 unusued;
                    if (_InFlight.TryRemove(txid.Hash, out unusued)) // Remove them from out inflight list 
                    {
                        if (_InFlight.Count == 0) // If inflight is zero 
                            StartScan(null); // Scan
                    }
                }
            }
        }

        private void HandleMerkleBlockPayload(MerkleBlockPayload merkleBlockPayload)
        {
            if (merkleBlockPayload != null)
            {
                if (!CheckFPRate(merkleBlockPayload))
                {
                    return;
                }
                _Logger.Information("Merkle block payload block time: {blockTime}", merkleBlockPayload.Object.Header.BlockTime);
                foreach (var txId in merkleBlockPayload.Object.PartialMerkleTree.GetMatchedTransactions())
                {
                    _TransactionsToBlock.AddOrUpdate(txId, merkleBlockPayload.Object, (k, v) => merkleBlockPayload.Object);
                    var tx = _walletSyncManager.GetKnownTransaction(txId);
                    if (tx != null)
                    {
                        NotifyWalletSyncManager(tx, merkleBlockPayload.Object);
                    }
                }

                var h = merkleBlockPayload.Object.Header.GetHash();
                uint256 unused;
                if (_InFlight.TryRemove(h, out unused))
                {
                    if (_InFlight.Count == 0)
                    {
                        UpdateCurrentPosition(h);
                        StartScan(unused);
                    }
                }
            }
        }

        private void UpdateCurrentPosition(uint256 h)
        {
            lock (_CurrentPositionlocker)
            {
                var chained = _Chain.GetBlock(h); // Get block belonging to this hash
                if (chained != null && !EarlierThanCurrentProgress(GetPartialLocator(chained))) // Make sure there is a block and the update isn't anterior
                {
                    _CurrentPosition = GetPartialLocator(chained); // Set the new location


                    var eventToPublish = new WalletPositionUpdatedEventArgs()
                    {
                        PreviousPosition = chained.Previous,
                        NewPosition = chained
                    };

                    _messageHub.Publish(eventToPublish);
                    _Logger.Information("Updated current position: {chainHeight}", _Chain.FindFork(_CurrentPosition).Height);
                }
            }
            
        }


        private bool EarlierThanCurrentProgress(BlockLocator locator)
        {
            return _Chain.FindFork(locator).Height < _Chain.FindFork(_CurrentPosition).Height;
        }


        private bool NotifyWalletSyncManager(Transaction tx, MerkleBlock blk)
        {

            bool hit = false;
            if (blk == null)
            {
                hit = _walletSyncManager.ProcessTransaction(tx);
            }
            else
            {

                var prev = _Chain.GetBlock(blk.Header.HashPrevBlock);
                if (prev != null)
                {
                    var header = new ChainedBlock(blk.Header, null, prev);
                    hit = _walletSyncManager.ProcessTransaction(tx, header, blk);
                }
                else
                {
                    hit = _walletSyncManager.ProcessTransaction(tx);
                }
            }

            Interlocked.Increment(ref _TotalReceived);
            if (!hit)
            {
                Interlocked.Increment(ref _FalsePositiveCount);
            }
            return hit;
        }

        private BlockLocator GetPartialLocator(ChainedBlock block)
        {
            //TODO: tries to create block locator all the way to genesis, we dont need all that, we just need the last locator, techincally.
            //But we can create an exponential locator the first block we do have.
            int nStep = 1;
            List<uint256> vHave = new List<uint256>();

            var pindex = block;
            while (pindex != null)
            {
                vHave.Add(pindex.HashBlock);
                // Stop when we have added the genesis block.
                if (pindex.Height == 0)
                    break;
                // Exponentially larger steps back, plus the genesis block.
                int nHeight = Math.Max(pindex.Height - nStep, 0);
                while (pindex.Height > nHeight)
                {
                    pindex = pindex.Previous;

                    if (pindex == null)
                    {
                        break;
                    }
                }
                if (vHave.Count > 10)
                    nStep *= 2;
            }

            var locators = new BlockLocator();
            locators.Blocks = vHave;
            return locators;
        }

        private bool CheckFPRate(MerkleBlockPayload merkleBlock)
        {
            var maxFPRate = FalsePositiveRate + MaximumFalsePositiveRateDifference;
            if (_TotalReceived > 100
                && ActualFalsePostiveRate >= maxFPRate)
            {
                var currentBlock = _Chain.GetBlock(merkleBlock.Object.Header.GetHash());
                if (currentBlock != null && currentBlock.Previous != null)
                    UpdateCurrentPosition(currentBlock.Previous.HashBlock);
                this.AttachedNode.DisconnectAsync("The actual false positive rate exceed MaximumFalsePositiveRate");
                return false;
            }
            return true;
        }

        private void ChangeOfAttachedNodeState(Node node, NodeState oldState)
        {
            if (node.State == NodeState.HandShaked)
            {
                SetBloomFilter();
                SendMessageAsync(new MempoolPayload());
            }
        }

        protected override void DetachCore()
        {
            AttachedNode.StateChanged -= ChangeOfAttachedNodeState;
            AttachedNode.MessageReceived -= MessagedRecivedOnAttachedNode;
        }


        public void SendMessageAsync(Payload payload)
        {
            var node = AttachedNode;
            if (node == null)
                return;

#pragma warning disable 612, 618
            if (_FilterState == FilterState.Loaded)
#pragma warning restore 612, 618

                node.SendMessageAsync(payload);
            else
            {
                _ActionsToFireWhenFilterIsLoaded.Add(() => node.SendMessageAsync(payload));
            }
        }
    }
}
