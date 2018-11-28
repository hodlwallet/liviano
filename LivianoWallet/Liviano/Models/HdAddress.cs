using Liviano.Enums;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Liviano.Models
{
    public class HdAddress
    {
        public HdAddress()
        {
            Transactions = new List<TransactionData>();
        }

        /// <summary>
        /// The index of the address.
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "P2WPKHScriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script P2WPKH_ScriptPubKey { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "P2PKHScriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script P2PKH_ScriptPubKey { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "P2SHP2WPKHScriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script P2SH_P2WPKH_ScriptPubKey { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "pubkey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script Pubkey { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }
        
        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "legacyAddress")]
        public string LegacyAddress { get; set; }

                /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "compatibilityAddress")]
        public string CompatilibityAddress { get; set; }

        /// <summary>
        /// A path to the address as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

        /// <summary>
        /// A list of transactions involving this address.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public ICollection<TransactionData> Transactions { get; set; }

        /// <summary>
        /// Determines whether this is a change address or a receive address.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if it is a change address; otherwise, <c>false</c>.
        /// </returns>
        public bool IsChangeAddress()
        {
            return HdOperations.IsChangeAddress(HdPath);
        }

        /// <summary>
        /// List all spendable transactions in an address.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TransactionData> UnspentTransactions()
        {
            if (Transactions == null)
            {
                return new List<TransactionData>();
            }

            return Transactions.Where(t => t.IsSpendable());
        }

        /// <summary>
        /// Get the address total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money confirmedAmount, Money unConfirmedAmount) GetSpendableAmount()
        {
            List<TransactionData> allTransactions = Transactions.ToList();

            long confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
            long total = allTransactions.Sum(t => t.SpendableAmount(false));

            return (confirmed, total - confirmed);
        }

        public IEnumerable<byte[]> GetTrackableAddressData(ScriptTypes scriptType)
        {
            switch (scriptType)
            {
                case ScriptTypes.Legacy:
                    return P2PKH_ScriptPubKey.ToOps().Select(x => x.PushData);
                case ScriptTypes.Segwit:
                    return P2WPKH_ScriptPubKey.ToOps().Select(x => x.PushData);
                case ScriptTypes.SegwitAndLegacy:
                    return P2WPKH_ScriptPubKey.ToOps().Select(x => x.PushData).Concat(P2PKH_ScriptPubKey.ToOps().Select(x => x.PushData));
                default:
                    throw new InvalidOperationException($"Unsupported script type:{Enum.GetName(typeof(ScriptTypes), scriptType)}");
            }
        }
    }
}
