using Refit;

namespace Liviano.Services
{
    public class Mempool
    {
        public IMempoolHttpService MempoolHttpService => RestService.For<IMempoolHttpService>(Constants.MEMPOOL_SPACE_2H_STATS);
    }
}
