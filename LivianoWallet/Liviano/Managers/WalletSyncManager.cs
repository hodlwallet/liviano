using System;
using System.Collections.Generic;
using System.Linq;
using Easy.MessageHub;
using Liviano.Enums;
using Liviano.Interfaces;
using Liviano.Models;
using NBitcoin;
using Serilog;

namespace Liviano.Managers
{
    public class WalletSyncManager : IWalletSyncManager
    {
        DateTime End, Start;

        WalletManager _WalletManager;

        ConcurrentChain _Chain;

        private bool _IsSyncedToChainTip = false;

        private uint _Tweak;

        private ILogger _Logger;

        private MessageHub _MessageHub;

        public WalletSyncManager(ILogger logger, WalletManager walletManager, ConcurrentChain chain)
        {
            _Chain = chain;
            _WalletManager = walletManager;
            _Logger = logger;
            _MessageHub = MessageHub.Instance;

            _MessageHub.Subscribe<WalletPositionUpdatedEventArgs>(HandleUpdatedWalletPosition);
        }

        private void HandleUpdatedWalletPosition(WalletPositionUpdatedEventArgs obj)
        {
            OnWalletPositionUpdated(obj);
        }

        public BlockLocator CurrentPosition { get; set; }

        public DateTimeOffset DateToStartScanningFrom { get; set; }

        public bool IsSynced { get => _IsSyncedToChainTip; }

        public BloomFilter CreateBloomFilter(double Fp, ScriptTypes scriptTypes, BloomFlags flags = BloomFlags.UPDATE_ALL)
        {
            var scriptCount = _WalletManager.GetAllAddressesByCoinType(CoinType.Bitcoin).Count(c => c.IsChangeAddress() == false);
            var filter = new BloomFilter(scriptCount == 0 ? 1 : scriptCount, Fp, _Tweak, flags);

            var toTrack = GetDataToTrack(scriptTypes).ToArray();
            foreach (var data in toTrack)
                filter.Insert(data);

            return filter;
        }

        public Transaction GetKnownTransaction(uint256 txId)
        {
            var transactionData =_WalletManager.GetAllTransactionsByCoinType(CoinType.Bitcoin).FirstOrDefault(x => x.Id == txId);
            if (transactionData == null)
            {
                return null;
            }
            Transaction tx = Transaction.Parse(transactionData.Hex, _WalletManager.Network);

            return tx;
        }

        public bool ProcessTransaction(Transaction transaction, ChainedBlock chainedBlock, Block block)
        {
            if (chainedBlock == null)
                return ProcessTransaction(transaction);

            return ProcessTransaction(transaction, chainedBlock, new MerkleBlock(block, new uint256[] { transaction.GetHash() }));
        }

        public bool ProcessTransaction(Transaction transaction)
        {
            return ProcessTransaction(transaction, null, null as MerkleBlock);
        }

        public bool ProcessTransaction(Transaction transaction, ChainedBlock chainedBlock, MerkleBlock proof)
        {
            if (chainedBlock != null)
            {
                if (proof == null)
                    throw new ArgumentNullException(nameof(proof));
                if (proof.Header.GetHash() != chainedBlock.Header.GetHash())
                    throw new InvalidOperationException("The chained block and the merkle block are different blocks");
                if (!proof.PartialMerkleTree.Check(chainedBlock.Header.HashMerkleRoot))
                    throw new InvalidOperationException("The MerkleBlock does not have the expected merkle root");
                if (!proof.PartialMerkleTree.GetMatchedTransactions().Contains(transaction.GetHash()))
                    throw new InvalidOperationException("The MerkleBlock does not contains the input transaction");

                _Logger.Information("Processing block time of: {0}", proof.Header.BlockTime);
            }

            var interesting = false;

            var addresses = _WalletManager.GetAllAddressesByCoinType(CoinType.Bitcoin).Where(x=> x.IsChangeAddress() == false);
            var outPoints = _WalletManager.GetAllSpendableTransactions(CoinType.Bitcoin, _Chain.Tip.Height).Select(x => x.ToOutPoint());

            var scripts = addresses.Select(x => x.ScriptPubKey);

            foreach (var txin in transaction.Inputs.AsIndexedInputs())
            {
                var outPointToLookFor = txin.PrevOut;

                if (outPoints.Contains(outPointToLookFor))
                {
                    _Logger.Information("Found tx with id: {transactionHash}!!!", transaction.GetHash());

                    _WalletManager.ProcessTransaction(
                        transaction,
                        null,
                        proof,
                        chainedBlock is null ? false : _Chain.Tip.Height >= chainedBlock.Height
                    );

                    interesting = true;
                }
            }

            foreach (var txout in transaction.Outputs.AsIndexedOutputs())
            {
                var scriptToSearchFor = txout.TxOut.ScriptPubKey;

                if (scripts.Contains(scriptToSearchFor))
                {
                    _Logger.Information("Found tx with id: {transactionHash}!!!", transaction.GetHash());

                    _WalletManager.ProcessTransaction(
                        transaction,
                        null,
                        proof,
                        chainedBlock is null ? false : _Chain.Tip.Height >= chainedBlock.Height
                     );

                    interesting = true;
                }

            }

            return interesting;
        }

        public void Scan(BlockLocator locator, DateTimeOffset dateToStartOn)
        {
            Start = DateTime.Now;
            if (DateToStartScanningFrom == default(DateTimeOffset) || dateToStartOn < DateToStartScanningFrom)
                DateToStartScanningFrom = dateToStartOn;
            if (CurrentPosition == null || EarlierThanCurrentProgress(locator))
                CurrentPosition = locator;
        }

        private bool EarlierThanCurrentProgress(BlockLocator locator)
        {
            return _Chain.FindFork(locator).Height < _Chain.FindFork(CurrentPosition).Height;
        }

        private IEnumerable<byte[]> GetDataToTrack(ScriptTypes scriptType)
        {
            var spendableTransactions = _WalletManager.GetAllSpendableTransactions(CoinType.Bitcoin, _Chain.Tip.Height);
            var addresses = _WalletManager.GetAllAddressesByCoinType(CoinType.Bitcoin).Where(x => x.IsChangeAddress() == false);
            var dataToTrack = spendableTransactions.Select(x => x.ToOutPoint().ToBytes()).Concat(addresses.SelectMany(x => x.GetTrackableAddressData())).Where(x => x != null);

            return dataToTrack;
        }

        private void UpdateTweak()
        {
            _Tweak = RandomUtils.GetUInt32();
        }

        public event EventHandler<WalletPositionUpdatedEventArgs> OnWalletPositionUpdate;

        public event EventHandler<ChainedBlock> OnWalletSyncedToTipOfChain;

        protected virtual void OnWalletPositionUpdated(WalletPositionUpdatedEventArgs e)
        {
            _WalletManager.UpdateLastBlockSyncedHeight(e.NewPosition);

            if (e.NewPosition.Height == _Chain.Tip.Height)
            {
                _Logger.Information("Wallet synced to tip of chain");
                End = DateTime.Now;
                _Logger.Information("START TIME: {startTime}", Start.ToLongTimeString());
                _Logger.Information("END TIME: {endTime}", End.ToLongTimeString());
                _Logger.Information("Time to sync (seconds): {difference}", End.Subtract(Start).TotalSeconds);
                _IsSyncedToChainTip = true;
                OnWalletSyncedToTipOfChain?.Invoke(this, e.NewPosition);
            }
            else
            {
                _IsSyncedToChainTip = false;
            }

            OnWalletPositionUpdate?.Invoke(this, e);
        }
    }
}
