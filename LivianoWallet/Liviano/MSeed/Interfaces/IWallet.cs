using System;
using System.Collections.Generic;

using NBitcoin;

using Newtonsoft.Json;

using Liviano.Utilities.JsonConverters;

namespace Liviano.MSeed.Interfaces
{
    public interface IWallet
    {
        [JsonProperty(PropertyName = "accountTypes")]
        string[] AccountTypes { get; }

        /// <summary>
        /// Id of the wallet, this will be in the filesystem
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "id")]
        string Id { get; }

        /// <summary>
        /// Name to show for the wallet, probably user provided or default
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }

        /// <summary>
        /// The network this wallets belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        Network Network { get; set; }

        /// <summary>
        /// Encrypted seed usually from a mnemonic
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "encryptedSeed")]
        string EncryptedSeed { get; set; }

        /// <summary>
        /// Tx ids linked to the wallet, usually this will be located also in the accounts,
        /// the wallet will find them in {walletId}/transactions
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "txIds")]
        List<string> TxIds { get; set; }

        /// <summary>
        /// Account ids of the wallet, these go under {walletId}/accounts
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "accountIds")]
        List<Dictionary<string, string>> AccountIds { get; set; }

        /// <summary>
        /// Init will create a new wallet initaliaing everything to their defaults,
        /// a new guid is created and the default for network is Main
        /// </summary>
        void Init(Network network = null, List<string> txIds = null, List<Dictionary<string, string>> accountIds = null);

        /// <summary>
        /// Gets a private key this method also caches it on memory
        /// </summary>
        /// <param name="password">Password to decript seed to, default ""</param>
        /// <param name="forcePasswordVerification">Force the password verification, avoid cache! Default false</param>
        /// <returns></returns>
        Key GetPrivateKey(string password = "", bool forcePasswordVerification = false);
    }
}