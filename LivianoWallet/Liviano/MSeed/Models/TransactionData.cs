using Liviano.Models;
using Liviano.Utilities.JsonConverters;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using System;

namespace Liviano.MSeed.Models
{
    public class TransactionData
    {
        /// <summary>
        /// Transaction id.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        [JsonConverter(typeof(Utilities.JsonConverters.UInt256JsonConverter))]
        public uint256 Id { get; set; }

        /// <summary>
        /// Account id the tx belongs to
        /// </summary>
        [JsonProperty(PropertyName = "accountId", NullValueHandling = NullValueHandling.Ignore)]
        public string AccountId { get; set; }

        /// <summary>
        /// The network this tx belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        Network Network { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amount", DefaultValueHandling = (long)0)]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money Amount { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amountSent", DefaultValueHandling = (long)0)]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money AmountSent { get; set; }

        /// <summary>
        /// The transaction total amount, sent and received by you.
        /// </summary>
        [JsonProperty(PropertyName = "totalAmount", DefaultValueHandling = (long)0)]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money TotalAmount { get; set; }

        /// <summary>
        /// The transaction total fees sent on this tx.
        /// </summary>
        [JsonProperty(PropertyName = "totalFees", DefaultValueHandling = (long)0)]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money TotalFees { get; set; }

        [JsonProperty(PropertyName = "isSend", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsSend { get; set; }

        [JsonProperty(PropertyName = "isReceive", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsReceive { get; set; }

        /// <summary>
        /// The index of this scriptPubKey in the transaction it is contained.
        /// </summary>
        /// <remarks>
        /// This is effectively the index of the output, the position of the output in the parent transaction.
        /// </remarks>
        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int Index { get; set; }

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockHeight { get; set; }

        /// <summary>
        /// The hash of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(Utilities.JsonConverters.UInt256JsonConverter))]
        public uint256 BlockHash { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(Liviano.Utilities.JsonConverters.DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// Gets or sets the Merkle proof for this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "merkleProof", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(BitcoinSerializableJsonConverter))]
        public PartialMerkleTree MerkleProof { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The script pub key for the address we sent to
        /// </summary>
        [JsonProperty(PropertyName = "sentToScriptPubKey", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script SentToScriptPubKey { get; set; }

        /// <summary>
        /// Hexadecimal representation of this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex { get; set; }

        /// <summary>
        /// Memo of the transaction to persist it locally
        /// </summary>
        [JsonProperty(PropertyName = "memo", NullValueHandling = NullValueHandling.Ignore)]
        public string Memo { get; set; }

        /// <summary>
        /// Propagation state of this transaction.
        /// </summary>
        /// <remarks>Assume it's <c>true</c> if the field is <c>null</c>.</remarks>
        [JsonProperty(PropertyName = "isPropagated", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsPropagated { get; set; }

        /// <summary>
        /// The details of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
        public SpendingDetails SpendingDetails { get; set; }

        /// <summary>
        /// Determines whether this transaction is confirmed.
        /// </summary>
        public bool IsConfirmed()
        {
            return BlockHeight != null;
        }

        /// <summary>
        /// Indicates an output is spendable.
        /// </summary>
        public bool IsSpendable()
        {
            return IsSend == false && SpendingDetails == null;
        }

        public Transaction GetTransaction()
        {
            return Transaction.Parse(Hex, Network);
        }

        public Money SpendableAmount(bool confirmedOnly)
        {
            // This method only returns a UTXO that has no spending output.
            // If a spending output exists (even if its not confirmed) this will return as zero balance.
            if (IsSpendable())
            {
                // If the 'confirmedOnly' flag is set check that the UTXO is confirmed.
                if (confirmedOnly && !IsConfirmed())
                {
                    return Money.Zero;
                }

                return Amount;
            }

            return Money.Zero;
        }
    }
}
