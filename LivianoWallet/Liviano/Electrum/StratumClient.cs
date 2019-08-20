//
// StratumClient.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 
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
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using NBitcoin;

using Liviano.Models;

namespace Liviano.Electrum
{
    public class StratumClient
    {
        public class Request
        {
            public int Id { get; set; }
            public string Method { get; set; }
            public IEnumerable Params { get; set; }
        }

        public class ResultAsString
        {
            public int Id { get; set; }
            public string Result { get; set; }
        }

        public class ServerVersionResult
        {
            public int Id { get; set; }
            public string[] Result { get; set; }
        }

        public class BlockchainScriptHashGetBalanceInnerResult
        {
            public long Confirmed { get; set; }
            public long Unconfirmed { get; set; }
        }

        public class BlockchainScriptHashGetBalanceResult
        {
            public int Id { get; set; }
            public BlockchainScriptHashGetBalanceInnerResult Result { get; set; }
        }

        public class BlockchainScriptHashListUnspentInnerResult
        {
            public string TxHash { get; set; }
            public int TxPos { get; set; }
            public long Value { get; set; }
            public long Height { get; set; }
        }

        public class BlockchainScriptHashListUnspentResult
        {
            public int Id { get; set; }
            public BlockchainScriptHashListUnspentInnerResult[] Result { get; set; }
        }

        public class BlockchainTransactionGetResult : ResultAsString { }

        public class BlockchainEstimateFeeResult
        {
            public int Id { get; set; }
            public double Result { get; set; }
        }

        public class BlockchainTransactionBroadcastResult : ResultAsString { }

        public class ErrorInnerResult
        {
            public string Message { get; set; }
            public int Code { get; set; }
        }

        public class ErrorResult
        {
            public int Id { get; set; }
            public ErrorInnerResult Error { get; set; }
        }

        JsonRpcTcpClient _JsonRpcClient;

        public StratumClient(List<Server> servers)
        {
            _JsonRpcClient = new JsonRpcTcpClient(servers);
        }

        public class PascalCase2LowercasePlusUnderscoreContractResolver : DefaultContractResolver
        {
            Regex pascalToUnderScoreRegex = new Regex(@"((?<=.)[A-Z][a-zA-Z]*)|((?<=[a-zA-Z])\d+)", RegexOptions.Multiline);
            string pascalToUnderScoreReplacementExpression = "_$1$2";

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
            var rawResponse = await _JsonRpcClient.Request(jsonRequest);

            if (string.IsNullOrEmpty(rawResponse))
            {
                throw new HttpListenerException(1, string.Format("Server '{0}' returned a null/empty JSON response to the request '{1}'", _JsonRpcClient.Host, jsonRequest));
            }

            try
            {
                return Deserialize<T>(rawResponse);
            }
            catch (Exception ex)
            {
                throw new HttpListenerException(1, ex.Message);
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
                throw new HttpListenerException(1, string.Format("Failed deserializing JSON response (to check for error) '{0}' to type '{1}'\n{2}", resultTrimmed, typeof(T).FullName, ex.Message));
            }

            if (maybeError != null && maybeError.Error != null) throw new HttpListenerException(1, string.Format("{0}\n{1}", maybeError.Error.Message, maybeError.Error.Code));

            T deserializedValue;

            try
            {
                deserializedValue = JsonConvert.DeserializeObject<T>(resultTrimmed, PascalCase2LowercasePlusUnderscoreConversionSettings());
            }
            catch (Exception ex)
            {
                throw new HttpListenerException(1, string.Format("Failed deserializing JSON response '{0}' to type '{1}'\n{2}", resultTrimmed, typeof(T).FullName, ex.Message));
            }

            if (deserializedValue == null)
            {
                throw new HttpListenerException(1, string.Format("Failed deserializing JSON response {0} to type {1} (result was null)", resultTrimmed, typeof(T).FullName));
            }

            return deserializedValue;
        }

        public async Task<BlockchainScriptHashGetBalanceResult> BlockchainScriptHashGetBalance(string scriptHash)
        {
            var obj = new Request { Id = 0, Method = "blockchain.scripthash.get_balance", Params = new List<string> { scriptHash } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainScriptHashGetBalanceResult>(json);
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
                throw new Exception(string.Format("Electrum Server's version disliked by .NET Version class: {0}\n{1}", versionStr, ex.Message));
            }
        }

        public async Task<System.Version> ServerVersion(string clientName, System.Version protocolVersion)
        {
            var obj = new Request { Id = 0, Method = "server.version", Params = new List<string> { clientName, protocolVersion.ToString() } };
            var json = Serialize(obj);

            ServerVersionResult resObj = await RequestInternal<ServerVersionResult>(json);

            return CreateVersion(resObj.Result[1]);
        }

        public async Task<BlockchainScriptHashListUnspentResult> BlockchainScriptHashListUnspent(string scriptHash)
        {
            var obj = new Request { Id = 0, Method = "blockchain.scripthash.listunspent", Params = new List<string> { scriptHash } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainScriptHashListUnspentResult>(json);
        }

        public async Task<BlockchainTransactionGetResult> BlockchainTransactionGet(string txhash)
        {
            var obj = new Request { Id = 0, Method = "blockchain.transaction.get", Params = new List<string> { txhash } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionGetResult>(json);
        }

        public async Task<BlockchainEstimateFeeResult> BlockchainEstimateFee(int numBlocksTarget)
        {
            var obj = new Request { Id = 0, Method = "blockchain.estimatefee", Params = new List<int> { numBlocksTarget } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainEstimateFeeResult>(json);
        }

        // From electrumx.readthedocs.io:
        // If the daemon rejects the transaction, the result is the error message string from the daemon,
        // as if the call were successful. The client needs to determine if an error occurred by comparing
        // the result to the expected transaction hash.
        public async Task<BlockchainTransactionBroadcastResult> BlockchainTransactionBroadcast(string txInHex)
        {
            var obj = new Request { Id = 0, Method = "blockchain.transaction.broadcast", Params = new List<string> { txInHex } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionBroadcastResult>(json);
        }

        public static string GetElectrumScriptHashFromAddress(string publicAddress, Network network)
        {
            var address = BitcoinAddress.Create(publicAddress, network);
            var sha = NBitcoin.Crypto.Hashes.SHA256(address.ScriptPubKey.ToBytes());
            var reversedSha = sha.Reverse().ToArray();
            return NBitcoin.DataEncoders.Encoders.Hex.EncodeData(reversedSha);
        }
    }
}
