using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Serilog;

namespace Liviano
{
    public class WalletSyncManager : IWalletSyncManager
    {
        WalletManager _walletManager;

        ConcurrentChain _chain;

        private uint _Tweak;

        private ILogger _Logger;

        public WalletSyncManager(WalletManager walletManager, ConcurrentChain chain, ILogger logger)
        {
            _chain = chain;
            _walletManager = walletManager;
            _Logger = logger;
        }

        public BlockLocator CurrentPosition { get; set; }

        public DateTimeOffset DateToStartScanningFrom { get; set; }

        public BloomFilter CreateBloomFilter(double Fp, ScriptTypes scriptTypes, BloomFlags flags = BloomFlags.UPDATE_ALL)
        {

            var scriptCount = _walletManager.Wallet.GetAllAddressesByCoinType(CoinType.Bitcoin).Count(c => c.IsChangeAddress() == false);
            var filter = new BloomFilter(scriptCount == 0 ? 1 : scriptCount, Fp, _Tweak, flags);

            var toTrack = GetDataToTrack(scriptTypes).ToArray();
            foreach (var data in toTrack)
                filter.Insert(data);

            return filter;
        }

        public Transaction GetKnownTransaction(uint256 txId)
        {
            var transactionData =_walletManager.Wallet.GetAllTransactionsByCoinType(CoinType.Bitcoin).FirstOrDefault(x => x.Id == txId);
            if (transactionData == null)
            {
                return null;
            }
            Transaction tx = new Transaction(transactionData.Hex);

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

            var addresses = _walletManager.Wallet.GetAllAddressesByCoinType(CoinType.Bitcoin).Where(x=> x.IsChangeAddress() == false);
            var outPoints = _walletManager.Wallet.GetAllSpendableTransactions(CoinType.Bitcoin, _chain.Tip.Height).Select(x => x.ToOutPoint());

            var legacyScripts = addresses.Select(x => x.P2WPKH_ScriptPubKey);
            var segWitScripts = addresses.Select(x => x.P2PKH_ScriptPubKey);
            var compatabilityScripts = addresses.Select(x => x.P2SH_P2WPKH_ScriptPubKey);

            var scripts = legacyScripts.Concat(segWitScripts).Concat(compatabilityScripts);

            foreach (var txin in transaction.Inputs.AsIndexedInputs())
            {
                var outPointToLookFor = txin.PrevOut;

                if (outPoints.Contains(outPointToLookFor))
                {
                    _Logger.Information("Found tx with id: {transactionHash}!!!", transaction.GetHash());

                    _walletManager.ProcessTransaction(transaction, null, proof);
                }
            }

            foreach (var txout in transaction.Outputs.AsIndexedOutputs())
            {
                var scriptToSearchFor = txout.TxOut.ScriptPubKey;

                if (scripts.Contains(scriptToSearchFor))
                {
                    _Logger.Information("Found tx with id: {transactionHash}!!!", transaction.GetHash());

                    _walletManager.ProcessTransaction(transaction, null, proof);
                }

            }
            //List<Operation> operations = null;
            //lock (cs)
            //{
            //    foreach (var txin in transaction.Inputs.AsIndexedInputs())
            //    {
            //        var key = TrackedOutpoint.GetId(txin.PrevOut);
            //        TrackedOutpoint match;
            //        if (_TrackedOutpoints.TryGetValue(key, out match))
            //        {
            //            TrackedScript parentMetadata;
            //            if (_TrackedScripts.TryGetValue(match.TrackedScriptId, out parentMetadata))
            //            {
            //                interesting = true;
            //                var op = Spent(parentMetadata, txin, match.Coin, chainedBlock, proof);
            //                if (op != null)
            //                {
            //                    operations = operations ?? new List<Operation>();
            //                    operations.Add(op);
            //                }
            //            }

            //        }
            //    }
            //    foreach (var txout in transaction.Outputs.AsIndexedOutputs())
            //    {
            //        var key = TrackedScript.GetId(txout.TxOut.ScriptPubKey);
            //        TrackedScript match;
            //        if (_TrackedScripts.TryGetValue(key, out match))
            //        {
            //            interesting = true;
            //            var op = Received(match, txout, chainedBlock, proof);
            //            if (op != null)
            //            {
            //                operations = operations ?? new List<Operation>();
            //                operations.Add(op);
            //            }
            //        }
            //    }
            //}
            //if (operations != null)
            //{
            //    FireCallbacks(operations);
            //}
            return interesting;
        }

        public void Scan(BlockLocator locator, DateTimeOffset dateToStartOn)
        {
            if (DateToStartScanningFrom == default(DateTimeOffset) || dateToStartOn < DateToStartScanningFrom)
                DateToStartScanningFrom = dateToStartOn;
            if (CurrentPosition == null || EarlierThanCurrentProgress(locator))
                CurrentPosition = locator;
        }

        private bool EarlierThanCurrentProgress(BlockLocator locator)
        {
            return _chain.FindFork(locator).Height < _chain.FindFork(CurrentPosition).Height;
        }

        private IEnumerable<byte[]> GetDataToTrack(ScriptTypes scriptType)
        {
            var spendableTransactions = _walletManager.Wallet.GetAllSpendableTransactions(CoinType.Bitcoin, _chain.Tip.Height);
            var addresses = _walletManager.Wallet.GetAllAddressesByCoinType(CoinType.Bitcoin).Where(x => x.IsChangeAddress() == false);
            var dataToTrack = spendableTransactions.Select(x => x.ToOutPoint().ToBytes()).Concat(addresses.SelectMany(x => x.GetTrackableAddressData(scriptType))).Where(x => x != null);

            return dataToTrack;
        }

        private void UpdateTweak()
        {
            _Tweak = RandomUtils.GetUInt32();
        }
    }
}
