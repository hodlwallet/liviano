using Liviano.Enums;
using Liviano.Models;
using NBitcoin;
using System;

namespace Liviano.Interfaces
{
    public interface IWalletSyncManager
    {
        BloomFilter CreateBloomFilter(double Fp,ScriptTypes scriptType, BloomFlags flags = BloomFlags.UPDATE_ALL);
        Transaction GetKnownTransaction(uint256 txId);
        bool ProcessTransaction(Transaction tx);
        bool ProcessTransaction(Transaction tx, ChainedBlock header, MerkleBlock blk);
        void Scan(BlockLocator locator, DateTimeOffset dateToStartOn);


        BlockLocator CurrentPosition { get; set; }
        DateTimeOffset DateToStartScanningFrom { get; set; }

        event EventHandler<WalletPostionUpdatedEventArgs> OnWalletPositionUpdate;

    }

}
