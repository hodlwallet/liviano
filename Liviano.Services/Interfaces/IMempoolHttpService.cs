using System.Collections.Generic;

using Refit;

using Liviano.Services.Models;

namespace Liviano.Services
{
    public interface IMempoolHttpService
    {
        [Get("/statistics/2h")]
        List<MempoolStatisticEntity> Get2hStatistics();
    }
}
