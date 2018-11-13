using NBitcoin;

namespace Liviano
{
    public interface IWalletSyncManager
    {
        BloomFilter CreateBloomFilter(double Fp);

    }
}
