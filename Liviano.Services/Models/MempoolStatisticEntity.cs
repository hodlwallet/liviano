using System.Collections.Generic;

using Newtonsoft.Json;

namespace Liviano.Services.Models
{
    public class MempoolStatisticEntity
    {
        [JsonProperty("added")]
        public long Added;

        [JsonProperty("vbytes_per_second")]
        public long VBytesPerSeconds;

        [JsonProperty("mempool_byte_weight")]
        public long MempoolByteWeight;

        [JsonProperty("total_fee")]
        public long TotalFee;

        [JsonProperty("vsizes")]
        public List<long> VSizes;
    }
}
