//
// ElectrumClient.cs
//
// Copyright (c) 2019 Hodl Wallet
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
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Liviano.Exceptions;

namespace Liviano.Electrum
{
    public class ElectrumClient
    {
        public static string CLIENT_NAME = $"{Version.ElectrumUserAgent}";
        public static System.Version REQUESTED_VERSION = new System.Version("1.4");
        public static int RequestId = -1;

        readonly JsonRpcClient jsonRpcClient;

        DateTimeOffset? lastCalledAt = null;
        public DateTimeOffset? LastCalledAt
        {
            get
            {
                return lastCalledAt;
            }

            set
            {
                lastCalledAt = value;
            }
        }

        public class Request
        {
            public int Id { get; set; }
            public string Method { get; set; }
            public IEnumerable Params { get; set; }
        }

        public class BaseResult { }

        public class ResultAsObject : BaseResult
        {
            public int Id { get; set; }
            public object Result { get; set; }
        }

        public class PingResult : ResultAsObject { }

        public class ResultAsString : BaseResult
        {
            public int Id { get; set; }
            public string Result { get; set; }
        }

        public class BannerResult : ResultAsString { }

        public class ServerVersionResult : BaseResult
        {
            public int Id { get; set; }
            public string[] Result { get; set; }
        }

        public class ServerPeersSubscribeResult : BaseResult
        {
            public int Id { get; set; }
            public List<List<object>> Result { get; set; }
        }

        public class ServerDonationAddressResult : ResultAsString { }

        public class ServerPingResult : BaseResult
        {
            public int Id { get; set; }
            public object Result { get; set; } = new object();
        }

        public class BlockchainBlockHeaderResult : BaseResult
        {
            public int Id { get; set; }
            public ResultAsString Result { get; set; }
        }

        public class BlockchainBlockHeadersInnerResult : BaseResult
        {
            public int Count { get; set; }
            public string Hex { get; set; }
            public int Max { get; set; }
        }

        public class BlockchainBlockHeadersResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainBlockHeadersInnerResult Result { get; set; }
        }

        public class BlockchainBlockHeadersWithCheckpointHeightInnerResult : BlockchainBlockHeadersInnerResult
        {
            public string[] Branch { get; set; }
            public string Root { get; set; }
        }

        public class BlockchainBlockHeadersWithCheckpointHeightResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainBlockHeadersWithCheckpointHeightInnerResult Result { get; set; }
        }

        public class BlockchainBlockHeaderWithCheckpointHeightInnerResult : BaseResult
        {
            public string Header { get; set; }
            public string[] Branch { get; set; }
            public string Root { get; set; }
        }

        public class BlockchainBlockHeaderWithCheckpointHeightResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainBlockHeaderWithCheckpointHeightInnerResult Result { get; set; }
        }

        public class BlockchainHeadersSubscribeInnerResult : BaseResult
        {
            public string Hex { get; set; }
            public int Height { get; set; }
        }

        public class BlockchainHeadersSubscribeResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainHeadersSubscribeInnerResult Result { get; set; }
        }

        public class BlockchainScriptHashGetBalanceInnerResult : BaseResult
        {
            public long Confirmed { get; set; }
            public long Unconfirmed { get; set; }
        }

        public class BlockchainScriptHashGetBalanceResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainScriptHashGetBalanceInnerResult Result { get; set; }
        }

        public class BlockchainScriptHashListUnspentInnerResult : BaseResult
        {
            public string TxHash { get; set; }
            public int TxPos { get; set; }
            public int Value { get; set; }
            public int Height { get; set; }
        }

        public class BlockchainScriptHashGetHistoryTxsResult : BaseResult
        {
            public string TxHash { get; set; }
            public int Height { get; set; }
            public int Fee { get; set; }
        }

        public class BlockchainScriptHashGetHistoryResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainScriptHashGetHistoryTxsResult[] Result { get; set; }
        }

        public class BlockchainScriptHashListUnspentResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainScriptHashListUnspentInnerResult[] Result { get; set; }
        }

        public class BlockchainTransactionGetResult : ResultAsString { }

        public class BlockchainTransactionGetVerboseInnerResult : BaseResult
        {
            public string Txid { get; set; }
            public string Hash { get; set; }
            public int Version { get; set; }
            public int Size { get; set; }
            public int Vsize { get; set; }
            public int Weight { get; set; }
            public long Locktime { get; set; }
            public BlockchainVinResult[] Vin { get; set; }
            public BlockchainVoutResult[] Vout { get; set; }
            public string Hex { get; set; }
            public string Blockhash { get; set; }
            public int Confirmations { get; set; }
            public int Time { get; set; }
            public int Blocktime { get; set; }
        }

        public class BlockchainTransactionGetVerboseResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainTransactionGetVerboseInnerResult Result { get; set; }
        }

        public class BlockchainTransactionGetMerkleInnerResult : BaseResult
        {
            public int BlockHeight { get; set; }
            public string[] Merkle { get; set; }
            public int Pos { get; set; }
        }

        public class BlockchainTransactionGetMerkleResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainTransactionGetMerkleInnerResult Result { get; set; }
        }

        public class BlockchainTransactionIdFromPosResult : ResultAsString { }

        public class BlockchainTransactionIdFromPosMerkleInnerResult : BaseResult
        {
            public string TxHash { get; set; }
            public string[] Merkle { get; set; }
        }

        public class BlockchainTransactionIdFromPosMerkleResult : BaseResult
        {
            public int Id { get; set; }
            public BlockchainTransactionIdFromPosMerkleInnerResult Result { get; set; }
        }

        public class MempoolGetFeeHistogramResult : BaseResult
        {
            public int Id { get; set; }
            public int[][] Result { get; set; }
        }

        public class BlockchainScriptSigResult : BaseResult
        {
            public string Asm { get; set; }
            public string Hex { get; set; }
        }

        public class BlockchainScriptPubKeyResult : BaseResult
        {
            public string Asm { get; set; }
            public string Hex { get; set; }
            public int ReqSigs { get; set; }
            public string Type { get; set; }
            public string[] Addresses { get; set; }
        }

        public class BlockchainVinResult : BaseResult
        {
            public string Txid { get; set; }
            public int Vout { get; set; }
            public BlockchainScriptSigResult ScriptSig { get; set; }
            public string[] Txinwitness { get; set; }
            public long Sequence { get; set; }
        }

        public class BlockchainVoutResult : BaseResult
        {
            public double Value { get; set; }
            public int N { get; set; }
            public BlockchainScriptPubKeyResult ScriptPubKey { get; set; }
        }

        public class BlockchainEstimateFeeResult : BaseResult
        {
            public int Id { get; set; }
            public double Result { get; set; }
        }

        public class BlockchainRelayFeeResult : BaseResult
        {
            public int Id { get; set; }
            public double Result { get; set; }
        }

        public class BlockchainTransactionBroadcastResult : ResultAsString { }

        public class ErrorInnerResult : BaseResult
        {
            public string Message { get; set; }
            public int Code { get; set; }
        }

        public class ErrorResult : BaseResult
        {
            public int Id { get; set; }
            public ErrorInnerResult Error { get; set; }
        }

        public class BaseNotification { }	

        public class BlockchainScriptHashSubscribeNotification : BaseNotification	
        {	
            public string Method { get; set; }	
            public string[] Params { get; set; }	
        }	

        public ElectrumClient(JsonRpcClient jsonRpcClient)
        {
            this.jsonRpcClient = jsonRpcClient;
        }

        public class PascalCase2LowercasePlusUnderscoreContractResolver : DefaultContractResolver
        {
            readonly Regex pascalToUnderScoreRegex = new Regex(@"((?<=.)[A-Z][a-zA-Z]*)|((?<=[a-zA-Z])\d+)", RegexOptions.Multiline);
            readonly string pascalToUnderScoreReplacementExpression = "_$1$2";

            protected override string ResolvePropertyName(string propertyName)
            {
                return pascalToUnderScoreRegex.Replace(propertyName, pascalToUnderScoreReplacementExpression).ToLower();
            }
        }

        public static JsonSerializerSettings PascalCase2LowercasePlusUnderscoreConversionSettings()
        {
            return new JsonSerializerSettings { ContractResolver = new PascalCase2LowercasePlusUnderscoreContractResolver() };
        }

        string Serialize(Request req)
        {
            return JsonConvert.SerializeObject(req, Formatting.None, PascalCase2LowercasePlusUnderscoreConversionSettings());
        }

        async Task<T> RequestInternal<T>(string jsonRequest)
        {
            var rawResponse = await jsonRpcClient.Request(jsonRequest);

            if (string.IsNullOrEmpty(rawResponse))
            {
                throw new ElectrumException(string.Format("Server '{0}' returned a null/empty JSON response to the request '{1}'", jsonRpcClient.Host, jsonRequest));
            }

            try
            {
                LastCalledAt = DateTimeOffset.UtcNow;

                return Deserialize<T>(rawResponse);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RequestInternal] There's an error??? {ex.Message}");

                throw new ElectrumException(ex.Message);
            }
        }

        public static T Deserialize<T>(string result)
        {
            var resultTrimmed = result.Trim();
            ErrorResult maybeError;

            try
            {
                maybeError = JsonConvert.DeserializeObject<ErrorResult>(resultTrimmed, PascalCase2LowercasePlusUnderscoreConversionSettings());
            }
            catch (Exception ex)
            {
                throw new ElectrumException(string.Format("Failed deserializing JSON response (to check for error) '{0}' to type '{1}'\n{2}", resultTrimmed, typeof(T).FullName, ex.Message));
            }

            if (maybeError != null && maybeError.Error != null) throw new ElectrumException(string.Format("{0}\n{1}", maybeError.Error.Message, maybeError.Error.Code));

            T deserializedValue;

            try
            {
                deserializedValue = JsonConvert.DeserializeObject<T>(resultTrimmed, PascalCase2LowercasePlusUnderscoreConversionSettings());
            }
            catch (Exception ex)
            {
                throw new ElectrumException(string.Format("Failed deserializing JSON response '{0}' to type '{1}'\n{2}", resultTrimmed, typeof(T).FullName, ex.Message));
            }

            if (deserializedValue == null)
            {
                throw new ElectrumException(string.Format("Failed deserializing JSON response {0} to type {1} (result was null)", resultTrimmed, typeof(T).FullName));
            }

            return deserializedValue;
        }

        public async Task<BlockchainBlockHeaderResult> BlockchainBlockHeader(int height)
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.block.header", Params = new List<int> { height, 0 } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainBlockHeaderResult>(json);
        }

        public async Task<BlockchainBlockHeaderWithCheckpointHeightResult> BlockchainBlockHeaderWithCheckpointHeight(int height, int cpHeight)
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.block.header", Params = new List<int> { height, cpHeight } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainBlockHeaderWithCheckpointHeightResult>(json);
        }

        public async Task<BlockchainBlockHeadersResult> BlockchainBlockHeaders(int height, int count = 2016)
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.block.headers", Params = new List<int> { height, count, 0 } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainBlockHeadersResult>(json);
        }

        public async Task<BlockchainHeadersSubscribeResult> BlockchainHeadersSubscribe()
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.headers.subscribe", Params = new List<string> { } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainHeadersSubscribeResult>(json);
        }

        public async Task<BlockchainBlockHeadersWithCheckpointHeightResult> BlockchainBlockHeaders(int height, int count, int cpHeight)
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.block.headers", Params = new List<int> { height, count, cpHeight } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainBlockHeadersWithCheckpointHeightResult>(json);
        }

        public async Task<BlockchainScriptHashGetBalanceResult> BlockchainScriptHashGetBalance(string scriptHash)
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.scripthash.get_balance", Params = new List<string> { scriptHash } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainScriptHashGetBalanceResult>(json);
        }

        public async Task<ServerPeersSubscribeResult> ServerPeersSubscribe()
        {
            var obj = new Request { Id = ++RequestId, Method = "server.peers.subscribe", Params = new List<string> { } };
            var json = Serialize(obj);

            return await RequestInternal<ServerPeersSubscribeResult>(json);
        }

        public async Task<string> ServerBanner()
        {
            var obj = new Request { Id = ++RequestId, Method = "server.banner", Params = new List<string> { } };
            var json = Serialize(obj);

            BannerResult resObj = await RequestInternal<BannerResult>(json);

            return resObj.Result;
        }

        public async Task<ServerPingResult> ServerPing()
        {
            var obj = new Request { Id = ++RequestId, Method = "server.ping", Params = new List<string> { } };
            var json = Serialize(obj);

            return await RequestInternal<ServerPingResult>(json);
        }

        public async Task<System.Version> ServerVersion()
        {
            return await ServerVersion(CLIENT_NAME, REQUESTED_VERSION);
        }

        public async Task<System.Version> ServerVersion(string clientName, System.Version protocolVersion)
        {
            var obj = new Request { Id = ++RequestId, Method = "server.version", Params = new List<string> { clientName, protocolVersion.ToString() } };
            var json = Serialize(obj);

            ServerVersionResult resObj = await RequestInternal<ServerVersionResult>(json);

            return CreateVersion(resObj.Result[1]);
        }

        public async Task<ServerDonationAddressResult> ServerDonationAddress()
        {
            var obj = new Request { Id = ++RequestId, Method = "server.donation_address", Params = new List<string> { } };
            var json = Serialize(obj);

            return await RequestInternal<ServerDonationAddressResult>(json);
        }

        public async Task<BlockchainScriptHashListUnspentResult> BlockchainScriptHashListUnspent(string scriptHash)
        {
            var obj = new Request { Id = RequestId, Method = "blockchain.scripthash.listunspent", Params = new List<string> { scriptHash } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainScriptHashListUnspentResult>(json);
        }

        public async Task<BlockchainScriptHashGetHistoryResult> BlockchainScriptHashGetHistory(string scriptHash)
        {
            var obj = new Request { Id = RequestId, Method = "blockchain.scripthash.get_history", Params = new List<string> { scriptHash } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainScriptHashGetHistoryResult>(json);
        }

        public async Task BlockchainScriptHashSubscribe(string scriptHash, Action<string> foundTxCallback)
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.scripthash.subscribe", Params = new List<string> { scriptHash } };
            var json = Serialize(obj);

            await jsonRpcClient.Subscribe(json, (res) => foundTxCallback(res));
        }

        public async Task<BlockchainTransactionGetResult> BlockchainTransactionGet(string txhash)
        {
            List<object> @params = new List<object> { txhash, false };

            var obj = new Request { Id = ++RequestId, Method = "blockchain.transaction.get", Params = @params };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionGetResult>(json);
        }

        public async Task<BlockchainTransactionGetVerboseResult> BlockchainTransactionGetVerbose(string txhash)
        {
            if (!string.IsNullOrEmpty(txhash))
            {
                throw new ElectrumException(
                    "Most servers don't support blockchain.transaction.get on verbose true, so don't use it at all, this forces it to fail"
                );
            }

            List<object> @params = new List<object> { txhash, true };

            var obj = new Request { Id = ++RequestId, Method = "blockchain.transaction.get", Params = @params };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionGetVerboseResult>(json);
        }

        public async Task<BlockchainTransactionGetMerkleResult> BlockchainTransactionGetMerkle(string txhash, int height)
        {
            List<object> @params = new List<object> { txhash, height };

            var obj = new Request { Id = ++RequestId, Method = "blockchain.transaction.get", Params = @params };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionGetMerkleResult>(json);
        }

        public async Task<BlockchainTransactionIdFromPosResult> BlockchainTransactionIdFromPos(int height, int txPos)
        {
            List<object> @params = new List<object> { height, txPos, false };

            var obj = new Request { Id = ++RequestId, Method = "blockchain.transaction.id_from_pos", Params = @params };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionIdFromPosResult>(json);
        }

        public async Task<BlockchainTransactionIdFromPosMerkleResult> BlockchainTransactionIdFromPosMerkle(int height, int txPos)
        {
            List<object> @params = new List<object> { height, txPos, true };

            var obj = new Request { Id = ++RequestId, Method = "blockchain.transaction.id_from_pos", Params = @params };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionIdFromPosMerkleResult>(json);
        }

        public async Task<MempoolGetFeeHistogramResult> MempoolGetFeeHistogram()
        {
            List<object> @params = new List<object> { };
            var obj = new Request { Id = ++RequestId, Method = "mempool.get_fee_histogram", Params = @params };
            var json = Serialize(obj);

            return await RequestInternal<MempoolGetFeeHistogramResult>(json);
        }

        public async Task<BlockchainEstimateFeeResult> BlockchainEstimateFee(int numBlocksTarget)
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.estimatefee", Params = new List<int> { numBlocksTarget } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainEstimateFeeResult>(json);
        }

        public async Task<BlockchainRelayFeeResult> BlockchainRelayFee()
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.relayfee", Params = new List<int> { } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainRelayFeeResult>(json);
        }

        // From electrumx.readthedocs.io:
        // If the daemon rejects the transaction, the result is the error message string from the daemon,
        // as if the call were successful. The client needs to determine if an error occurred by comparing
        // the result to the expected transaction hash.
        public async Task<BlockchainTransactionBroadcastResult> BlockchainTransactionBroadcast(string txInHex)
        {
            var obj = new Request { Id = ++RequestId, Method = "blockchain.transaction.broadcast", Params = new List<string> { txInHex } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionBroadcastResult>(json);
        }

        System.Version CreateVersion(string versionStr)
        {
            string correctedVersion = versionStr;

            if (versionStr.EndsWith("+", StringComparison.Ordinal))
            {
                correctedVersion = versionStr.Substring(0, versionStr.Length - 1);
            }

            try
            {
                return new System.Version(correctedVersion);
            }
            catch (Exception ex)
            {
                throw new ElectrumException(string.Format("Electrum Server's version disliked by .NET Version class: {0}\n{1}", versionStr, ex.Message));
            }
        }
    }
}
