using System.Collections.Generic;

using Newtonsoft.Json;

using NBitcoin;

using Liviano.Utilities.JsonConverters;

namespace Liviano.MSeed.Interfaces
{
    public interface IAccount
    {
        [JsonProperty(PropertyName = "id")]
        string Id { get; set; }

        [JsonProperty(PropertyName = "accountType")]
        string AccountType { get; }

        [JsonProperty(PropertyName = "walletId")]
        string WalletId { get; set; }

        /// <summary>
        /// The network this wallets belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        Network Network { get; set; }

        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }

        [JsonProperty(PropertyName = "txIds")]
        List<string> TxIds { get; set; }

        /// <summary>
        /// Gets 1 receiving address
        /// </summary>
        /// <returns></returns>
        BitcoinAddress GetReceiveAddress();

        /// <summary>
        /// Gets n receiving addresses
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        BitcoinAddress[] GetReceiveAddress(int n);

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
        BitcoinAddress[] GetChangeAddress(int n);
    }
}