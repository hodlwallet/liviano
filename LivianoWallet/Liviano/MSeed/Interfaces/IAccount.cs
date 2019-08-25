using System.Collections.Generic;

using Newtonsoft.Json;

using NBitcoin;

namespace Liviano.MSeed.Interfaces
{
    public interface IAccount
    {
        [JsonProperty(PropertyName = "id")]
        string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }

        [JsonProperty(PropertyName = "addressCount")]
        int AddressCount { get; set; }

        [JsonProperty(PropertyName = "txIds")]
        List<string> TxIds { get; set; }

        /// <summary>
        /// Gets 1 receiving address
        /// </summary>
        /// <returns></returns>
        BitcoinAddress GetReceivingAddress();

        /// <summary>
        /// Gets n receiving addresses
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        BitcoinAddress GetReceivingAddress(int n);

        /// <summary>
        /// Gets 1 change address
        /// </summary>
        /// <returns></returns>
        BitcoinAddress GetChangeAddress();

        /// <summary>
        /// Gets n change addresses
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        BitcoinAddress GetChangeAddress(int n);
    }
}