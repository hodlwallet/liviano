//
// Wallet.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Linq;

using Newtonsoft.Json;

using NBitcoin;
using NBitcoin.JsonConverters;

using Liviano.Utilities.JsonConverters;
using Liviano.Interfaces;
using static Liviano.Electrum.ElectrumClient;
using Liviano.Exceptions;

namespace Liviano.Models
{
    public class Tx
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

        [JsonIgnore]
        public IAccount Account { get; set; }

        /// <summary>
        /// The network this tx belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        public Network Network { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        [JsonProperty(PropertyName = "amountReceived", DefaultValueHandling = (long)0)]
        [JsonConverter(typeof(MoneyJsonConverter))]
        public Money AmountReceived { get; set; }

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

        /// <summary>
        /// This means is a send, the output that belongs to you was sent to a change (internal) address
        /// </summary>
        [JsonProperty(PropertyName = "isSend", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsSend { get; set; }

        /// <summary>
        /// This means is receive, the output that is to a receive (external) address
        /// </summary>
        [JsonProperty(PropertyName = "isReceive", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsReceive { get; set; }

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
        [JsonProperty(PropertyName = "sentScriptPubKey", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script SentScriptPubKey { get; set; }

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
            return IsSend == false;
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

                return AmountReceived;
            }

            return Money.Zero;
        }

        public static Tx CreateFromHex(string hex, IAccount account, Network network, BitcoinAddress[] internalAddresses, BitcoinAddress[] externalAddresses)
        {
            // NBitcoin Transaction object
            var transaction = Transaction.Parse(hex, network);
            var tx = new Tx()
            {
                Id = transaction.GetHash(),
                Account = account,
                AccountId = account.Id,
                Network = network,
                Hex = hex
            };

            // Decide if the tx is a send tx or a receive tx
            var addresses = transaction.Outputs.Select((txOut) => txOut.ScriptPubKey.GetDestinationAddress(network));
            foreach (var addr in addresses)
            {
                if (externalAddresses.Contains(addr))
                {
                    tx.IsReceive = true;
                    tx.IsSend = false;
                    break;
                }

                if (internalAddresses.Contains(addr))
                {
                    tx.IsSend = true;
                    tx.IsReceive = false;
                    break;
                }
            }

            // Amounts.
            tx.TotalAmount = transaction.TotalOut;

            if (tx.IsReceive)
            {
                // When sending every output that belongs to our external addresses
                // gets summed in
                tx.AmountReceived = transaction.Outputs.Sum((@out) =>
                {
                    var outAddr = @out.ScriptPubKey.GetDestinationAddress(network);

                    if (externalAddresses.Contains(outAddr))
                        return @out.Value;

                    return Money.Zero;
                });
            }
            else if (tx.IsSend)
            {
                // When 
                tx.AmountSent = transaction.Outputs.Sum((@out) =>
                {
                    var outAddr = @out.ScriptPubKey.GetDestinationAddress(network);

                    if (!internalAddresses.Contains(outAddr))
                        return @out.Value;

                    return Money.Zero;
                });
            }
            else
            {
                throw new WalletException("Could not decide if the tx is send or receive...");
            }

            tx.BlockHash = 0; // TODO
            tx.IsPropagated = true; // TODO
            tx.TotalFees = Money.Zero; // TODO

            return tx;
        }

        public static Tx CreateFromElectrumResult(BlockchainTransactionGetVerboseResult result, IAccount account, Network network)
        {
            var tx = new Tx()
            {
                Id = uint256.Parse(result.Txid),
                Account = account,
                AccountId = account.Id,
                Network = network,
                Hex = result.Hex
            };

            return tx;
        }
    }
}
