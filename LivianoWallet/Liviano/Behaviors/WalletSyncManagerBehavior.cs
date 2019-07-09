using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Serilog;

using Easy.MessageHub;

using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.SPV;

using Liviano.Enums;
using Liviano.Interfaces;
using Liviano.Models;

namespace Liviano.Behaviors
{
    public class WalletSyncManagerBehavior : NodeBehavior
    {
        const double FALSE_POSITIVE_RATE_DEFAULT = 0.000000000000000000001;

        IWalletSyncManager _WalletSyncManager;

        long _FalsePositiveCount = 0;

        long _TotalReceived = 0;

        object _Lock = new object();

        object _CurrentPositionlocker = new object();

        DateTimeOffset _SkipBefore { get { return _WalletSyncManager.DateToStartScanningFrom; } set { _WalletSyncManager.DateToStartScanningFrom = value; } }

        BlockLocator _CurrentPosition { get { return _WalletSyncManager.CurrentPosition; } set { _WalletSyncManager.CurrentPosition = value; } }

        ConcurrentChain _Chain;

        ConcurrentChain _ExplicitChain;

        ConcurrentDictionary<uint256, uint256> _InFlight = new ConcurrentDictionary<uint256, uint256>();

        BoundedDictionary<uint256, MerkleBlock> _TransactionsToBlock = new BoundedDictionary<uint256, MerkleBlock>(1000);

        volatile PingPayload _PreviousPing;

        ConcurrentBag<Action> _ActionsToFireWhenFilterIsLoaded;

        ScriptTypes _ScriptType;

        ILogger _Logger;

        MessageHub _MessageHub;

        /// <summary>
        /// The maximum accepted false positive rate difference, the node will be disconnected if the actual false positive rate is higher than FalsePositiveRate + MaximumFalsePositiveRateDifference.
        /// </summary>
        public double MaximumFalsePositiveRateDifference { get; set; } = 0.1;

// Suppresses warnings for obsolete use of SPV code
#pragma warning disable 612, 618
        public FilterState _FilterState { get; private set; }
#pragma warning restore 612, 618

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

        public WalletSyncManagerBehavior(ILogger logger, IWalletSyncManager walletSyncManager, ScriptTypes scriptType = ScriptTypes.P2WPKH, ConcurrentChain chain = null, double fpRate = 0.00)
        {
            _Logger = logger;

            _WalletSyncManager = walletSyncManager ?? throw new ArgumentNullException(nameof(walletSyncManager));
            FalsePositiveRate = fpRate > 0.00 ? fpRate : FALSE_POSITIVE_RATE_DEFAULT;
            _Chain = chain;
            _ExplicitChain = chain;
            _ScriptType = scriptType;
            _ActionsToFireWhenFilterIsLoaded = new ConcurrentBag<Action>();

            _MessageHub = MessageHub.Instance;
        }

        public override object Clone()
        {
            var clone = new WalletSyncManagerBehavior(_Logger, _WalletSyncManager, _ScriptType, _ExplicitChain);

            clone.FalsePositiveRate = FalsePositiveRate;
            clone._SkipBefore = _SkipBefore;
            clone._CurrentPosition = _CurrentPosition;

            return clone;
        }

        public void RefreshBloomFilter()
        {
            SetBloomFilter();
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

        protected override void DetachCore()
        {
            AttachedNode.StateChanged -= ChangeOfAttachedNodeState;
            AttachedNode.MessageReceived -= MessagedRecivedOnAttachedNode;
        }

        void SetBloomFilter()
        {
            var node = AttachedNode;
            if (node != null) //Insure we have a node
            {
                _PreviousPing = null; //Set to null
                var filter = _WalletSyncManager.CreateBloomFilter(FalsePositiveRate, _ScriptType); //Create bloom filter

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

        void MessagedRecivedOnAttachedNode(NBitcoin.Protocol.Node node, NBitcoin.Protocol.IncomingMessage message)
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

        void StartScan(object p)
        {
            var node = AttachedNode;
            if (_FilterState != FilterState.Loaded)
            {
                _ActionsToFireWhenFilterIsLoaded.Add(() => StartScan(p));
                return;
            }
            if (!IsScanning(node))
            {
                if (Monitor.TryEnter(_Lock))
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
                        Monitor.Exit(_Lock);
                    }
                }
            }
        }

        bool IsScanning(Node node)
        {
            return _InFlight.Count != 0 || _CurrentPosition == null || node == null;
        }

        void HandlePongPayload(PongPayload pongPayload)
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

        void HandleTxPayload(TxPayload txPayload)
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

        void HandleInvPayload(InvPayload invPayload, Node node)
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

        void HandleNotFoundPayLoad(NotFoundPayload notFoundPayload)
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

        void HandleMerkleBlockPayload(MerkleBlockPayload merkleBlockPayload)
        {
            if (merkleBlockPayload != null)
            {
                if (!CheckFPRate(merkleBlockPayload))
                {
                    return;
                }

                _Logger.Information(
                    "{attachedNodeInfoString} Merkle Block Payload: Block Time: {blockTime}. Height: {height}. Hash: {hash}. TXs: {transactionCount}",
                    AttachedNode.InfoString(),
                    merkleBlockPayload.Object.Header.BlockTime.DateTime,
                    _Chain.GetBlock(merkleBlockPayload.Object.Header.GetHash())?.Height,
                    merkleBlockPayload.Object.Header.GetHash(),
                    merkleBlockPayload.Object.PartialMerkleTree.TransactionCount
                );

                foreach (var txId in merkleBlockPayload.Object.PartialMerkleTree.GetMatchedTransactions())
                {
                    _TransactionsToBlock.AddOrUpdate(txId, merkleBlockPayload.Object, (k, v) => merkleBlockPayload.Object);
                    var tx = _WalletSyncManager.GetKnownTransaction(txId);
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

        void UpdateCurrentPosition(uint256 h)
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

                    _MessageHub.Publish(eventToPublish);
                    _Logger.Information("Updated current position: {chainHeight}", _Chain.FindFork(_CurrentPosition).Height);
                }
            }
            
        }

        bool EarlierThanCurrentProgress(BlockLocator locator)
        {
            return _Chain.FindFork(locator).Height < _Chain.FindFork(_CurrentPosition).Height;
        }


        bool NotifyWalletSyncManager(Transaction tx, MerkleBlock blk)
        {

            bool hit = false;
            if (blk == null)
            {
                hit = _WalletSyncManager.ProcessTransaction(tx);
            }
            else
            {

                var prev = _Chain.GetBlock(blk.Header.HashPrevBlock);
                if (prev != null)
                {
                    var header = new ChainedBlock(blk.Header, null, prev);
                    hit = _WalletSyncManager.ProcessTransaction(tx, header, blk);
                }
                else
                {
                    hit = _WalletSyncManager.ProcessTransaction(tx);
                }
            }

            Interlocked.Increment(ref _TotalReceived);
            if (!hit)
            {
                Interlocked.Increment(ref _FalsePositiveCount);
            }
            return hit;
        }

        BlockLocator GetPartialLocator(ChainedBlock block)
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

        bool CheckFPRate(MerkleBlockPayload merkleBlock)
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

        void ChangeOfAttachedNodeState(Node node, NodeState oldState)
        {
            if (node.State == NodeState.HandShaked)
            {
                SetBloomFilter();
                SendMessageAsync(new MempoolPayload());
            }
        }
    }
}
