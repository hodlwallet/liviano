using Refit;

namespace Liviano.Services
{
    public interface MempoolHttpService
    {
        [Get("/statistics/2h")]
        string Get2hStatistics();
    }
}
