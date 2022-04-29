//
// Tx.cs
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
using System.Diagnostics;
using System.Linq;

using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Liviano.Interfaces;
using Liviano.Extensions;

using static Liviano.Electrum.ElectrumClient;
using System.Collections.Generic;

namespace Liviano.Models
{
    public enum TxType
    {
        Partial,
        Send,
        Receive
    }

    public class Tx
    {
        /// <summary>
        /// Transaction id.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public uint256 Id { get; set; }

        /// <summary>
        /// The network this tx belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        public Network Network { get; set; }

        /// <summary>
        /// The account the tx belongs to.
        /// </summary>
        [JsonIgnore]
        public IAccount Account { get; set; }

        /// <summary>
        /// The transaction amount sent or received by us.
        /// </summary>
        [JsonProperty(PropertyName = "amount", DefaultValueHandling = (long)0)]
        public Money Amount { get; set; }

        /// <summary>
        /// The transaction fees sent by us, 0 for a receive
        /// </summary>
        [JsonProperty(PropertyName = "fees", DefaultValueHandling = (long)0)]
        public Money Fees { get; set; }

        /// <summary>
        /// The type of the tx, <see cref="TxType" /> for mode details
        /// </summary>
        [JsonProperty(PropertyName = "txType", ItemConverterType = typeof(StringEnumConverter))]
        public TxType Type { get; set; } = TxType.Partial;

        /// <summary>
        /// The height of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "height", DefaultValueHandling = (long)0)]
        public long Height { get; set; }

        /// <summary>
        /// The hash of the block including this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "blockhash", NullValueHandling = NullValueHandling.Ignore)]
        public uint256 Blockhash { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "createdAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// The script pub key for this address.
        /// </summary>
        [JsonProperty(PropertyName = "scriptPubKey", NullValueHandling = NullValueHandling.Ignore)]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// Other script pub keys
        /// </summary>
        [JsonProperty(PropertyName = "otherScriptPubKeys", NullValueHandling = NullValueHandling.Ignore)]
        public List<Script> OtherScriptPubKeys { get; set; }

        /// <summary>
        /// Check if the tx is rbf
        /// </summary>
        [JsonProperty(PropertyName = "isRBF", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsRBF { get; set; }

        /// <summary>
        /// Hexadecimal representation of this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
        public string Hex { get; set; }

        /// <summary>
        /// Replaced id
        /// </summary>
        [JsonProperty(PropertyName = "replacedId", NullValueHandling = NullValueHandling.Ignore)]
        public uint256 ReplacedId { get; set; }

        /// <summary>
        /// The NBitcoin transaction
        /// </summary>
        [JsonIgnore]
        public Transaction Transaction { get; set; }

        public Transaction GetTransaction()
        {
            if (string.IsNullOrEmpty(Hex)) return null;

            var tx = Transaction.Parse(Hex, Network);

            Transaction = tx;

            return Transaction;
        }

        public static Tx CreateFrom(string id, long height, long fees, IAccount account)
        {
            var tx = new Tx()
            {
                Id = uint256.Parse(id),
                Type = TxType.Partial,
                Account = account,
                Network = account.Network,
                Height = height,
                Fees = new Money(fees)
            };

            return tx;
        }

        public static Tx CreateFrom(
                string hex,
                long height,
                BlockHeader header,
                IAccount account)
        {
            Debug.WriteLine($"[CreateFromHex] Creating tx from hex: {hex}!");

            // NBitcoin Transaction object
            var transaction = Transaction.Parse(hex, account.Network);

            var tx = new Tx
            {
                Id = transaction.GetHash(),
                Account = account,
                Network = account.Network,
                Hex = hex,
                Transaction = transaction,
                IsRBF = transaction.RBF,
                Height = height,
                Type = TxType.Partial
            };

            if (header is not null)
            {
                tx.Blockhash = header.GetHash();
                tx.CreatedAt = header.BlockTime;
            }

            if (account.ContainInputs(transaction.Inputs)) tx.Type = TxType.Send;
            else tx.Type = TxType.Receive;

            // Amount
            if (tx.Type == TxType.Receive)
            {
                tx.Amount = new Money(transaction.Outputs.Where((@out) =>
                {
                    var outAddr = @out.ScriptPubKey.GetDestinationAddress(account.Network);

                    return account.IsReceive(outAddr) || account.IsChange(outAddr);
                }).Select((@out) => @out.Value.ToDecimal(MoneyUnit.BTC)).Sum(), MoneyUnit.BTC);
            }
            else
            {
                tx.Amount = new Money(transaction.Outputs.Where((@out) =>
                {
                    var outAddr = @out.ScriptPubKey.GetDestinationAddress(account.Network);

                    return !account.IsChange(outAddr) && !account.IsReceive(outAddr);
                }).Select((@out) => @out.Value.ToDecimal(MoneyUnit.BTC)).Sum(), MoneyUnit.BTC);
            }

            tx.Fees = account.GetOutValueFromTxInputs(transaction.Inputs) - transaction.TotalOut;

            return tx;
        }

        public bool UpdateTx()
        {
            var prevType = Type;
            var prevAmount = Amount;
            var prevFees = Fees;

            if (Account.ContainInputs(Transaction.Inputs)) Type = TxType.Send;
            else Type = TxType.Receive;

            // Amount
            if (Type == TxType.Receive)
            {
                Amount = new Money(Transaction.Outputs.Where((@out) =>
                {
                    var outAddr = @out.ScriptPubKey.GetDestinationAddress(Account.Network);

                    return Account.IsReceive(outAddr) || Account.IsChange(outAddr);
                }).Select((@out) => @out.Value.ToDecimal(MoneyUnit.BTC)).Sum(), MoneyUnit.BTC);
            }
            else
            {
                Amount = new Money(Transaction.Outputs.Where((@out) =>
                {
                    var outAddr = @out.ScriptPubKey.GetDestinationAddress(Account.Network);

                    return !Account.IsChange(outAddr) && !Account.IsReceive(outAddr);
                }).Select((@out) => @out.Value.ToDecimal(MoneyUnit.BTC)).Sum(), MoneyUnit.BTC);
            }

            Fees = Account.GetOutValueFromTxInputs(Transaction.Inputs) - Transaction.TotalOut;

            return !(prevFees == Fees && prevAmount == Amount && prevType == Type);
        }

        public static Tx CreateFromElectrumResult(BlockchainTransactionGetVerboseResult result, IAccount account, Network network)
        {
            var tx = new Tx
            {
                Id = uint256.Parse(result.Result.Txid),
                Account = account,
                Network = network,
                Hex = result.Result.Hex
            };

            return tx;
        }
    }
}
