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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using NBitcoin;

using Liviano.Models;
using Liviano.Extensions;
using Liviano.Exceptions;

namespace Liviano.Electrum
{
    public class ElectrumClient
    {
        public static string CLIENT_NAME = Version.ToString(); // Liviano (X.Y.Z)
        public static System.Version REQUESTED_VERSION = new System.Version("1.4");

        const int NUMBER_OF_RECENT_SERVERS = 4;

        JsonRpcClient _JsonRpcClient;

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
            public long Value { get; set; }
            public long Height { get; set; }
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

        public class BlockchainScriptSigResult : BaseResult
        {
            public string Asm { get; set; }
            public string Hex { get; set; }
        }

        public class BlockchainScriptPubKeyResult : BaseResult
        {
            public string Asm { get; set; }
            public string Hex { get; set; }
            public int Reqsigs { get; set; }
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

        public class BlockchainTransactionGetVerboseResult : BaseResult
        {
            public string Txid { get; set; }
            public string Hash { get; set; }
            public int Version { get; set; }
            public int Size { get; set; }
            public int Vsize { get; set; }
            public int Weight { get; set; }
            public int Locktime { get; set; }
            public BlockchainVinResult[] Vin { get; set; }
            public BlockchainVoutResult[] Vout { get; set; }
            public string Hex { get; set; }
            public string Blockhash { get; set; }
            public int Confirmations { get; set; }
            public int Time { get; set; }
            public int Blocktime { get; set; }
        }

        public class BlockchainEstimateFeeResult : BaseResult
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

        public ElectrumClient(List<Server> servers)
        {
            _JsonRpcClient = new JsonRpcClient(servers);
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
                throw new ElectrumException(string.Format("Server '{0}' returned a null/empty JSON response to the request '{1}'", _JsonRpcClient.Host, jsonRequest));
            }

            try
            {
                return Deserialize<T>(rawResponse);
            }
            catch (Exception ex)
            {
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

        public async Task<BlockchainScriptHashGetBalanceResult> BlockchainScriptHashGetBalance(string scriptHash)
        {
            var obj = new Request { Id = 0, Method = "blockchain.scripthash.get_balance", Params = new List<string> { scriptHash } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainScriptHashGetBalanceResult>(json);
        }

        public async Task<string> ServerBanner()
        {
            var obj = new Request { Id = 0, Method = "server.banner", Params = new List<string> { } };
            var json = Serialize(obj);

            BannerResult resObj = await RequestInternal<BannerResult>(json);

            return resObj.Result;
        }

        public async Task<object> ServerPing()
        {
            var obj = new Request { Id = 0, Method = "server.ping", Params = new List<string> { } };
            var json = Serialize(obj);

            BannerResult resObj = await RequestInternal<BannerResult>(json);

            return resObj.Result;
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

        public async Task<BlockchainScriptHashGetHistoryResult> BlockchainScriptHashGetHistory(string scriptHash)
        {
            var obj = new Request { Id = 0, Method = "blockchain.scripthash.get_history", Params = new List<string> { scriptHash } };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainScriptHashGetHistoryResult>(json);
        }

        public async Task<BlockchainTransactionGetResult> BlockchainTransactionGet(string txhash)
        {
            List<object> @params = new List<object> { txhash, false };

            var obj = new Request { Id = 0, Method = "blockchain.transaction.get", Params = @params };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionGetResult>(json);
        }

        public async Task<BlockchainTransactionGetVerboseResult> BlockchainTransactionGetVerbose(string txhash)
        {
            List<object> @params = new List<object> { txhash, true };

            var obj = new Request { Id = 0, Method = "blockchain.transaction.get", Params = @params };
            var json = Serialize(obj);

            return await RequestInternal<BlockchainTransactionGetVerboseResult>(json);
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
            var bitcoinAddress = BitcoinAddress.Create(publicAddress, network);

            return bitcoinAddress.ToScriptHash().ToHex();
        }

        /// <summary>
        /// Gets a list of recently conneted servers, these would be ready to connect
        /// </summary>
        /// <returns>a <see cref="List{Server}"/> of the recent servers</returns>
        public static List<Server> GetRecentlyConnectedServers(Network network = null)
        {
            if (network is null) network = Network.Main;

            List<Server> recentServers = new List<Server>();
            var fileName = Path.GetFullPath(GetRecentlyConnectedServersFileName(network));

            if (!File.Exists(fileName))
                return recentServers;

            var content = File.ReadAllText(fileName);

            recentServers.AddRange(JsonConvert.DeserializeObject<Server[]>(content));

            return recentServers;
        }

        /// <summary>
        /// Creates the file of the recently connected,
        /// this functions picks up to <see cref="NUMBER_OF_RECENT_SERVERS"/> servers at the time, and try to send
        /// a server.version() to the electrum server, if we get 1.4 then we're good,
        /// the server get added, this all happens and we wait for it to finish,
        /// then we get out once we get <see cref="NUMBER_OF_RECENT_SERVERS"/> servers
        /// </summary>
        /// <param name="network">Bitcoin network to connect to, <see cref="Network.Main"/> is the default</param>
        public static void PopulateRecentlyConnectedServers(Network network = null)
        {
            if (network is null) network = Network.Main;

            List<Server> connectedServers = new List<Server>();

            // Get network list of servers
            string serversFileName = GetLocalConfigFilePath(
                "Electrum",
                "servers",
                $"{network.Name.ToLower()}.json"
            );
            if (!File.Exists(serversFileName))
                throw new ArgumentException($"Invalid network: {network.Name}");

            // Get the servers list from the file.
            var json = File.ReadAllText(serversFileName);
            var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            var servers = ElectrumServers.FromDictionary(data).Servers.CompatibleServers();

            // Able to pop servers so we can check their version
            var popableServers = new List<Server>();
            popableServers.AddRange(servers);

            // We need to get a ranom server from the list, this will be the index
            var rng = new Random();

#pragma warning disable IDE0067 // Dispose objects before losing scope
            var cts = new CancellationTokenSource();
#pragma warning restore IDE0067 // Dispose objects before losing scope

            // Lock for the servers collection
            var _lock = new object();
            while (popableServers.Count > 0)
            {
                var tasks = new List<Task>();

                // pick 5 randos
                int count = 0;
                var randomServers = new List<Server>();
                while (count < NUMBER_OF_RECENT_SERVERS) // Remove this amount from the the popable
                {
                    if (popableServers.Count == 0) break;

                    var i = rng.Next(popableServers.Count);
                    var s = popableServers[i];

                    if (!randomServers.Contains(s))
                    {
                        randomServers.Add(s);
                        popableServers.Remove(s);
                    }
                    count += 1;
                }

                // Get out if we don't have any serves to process anymore
                if (popableServers.Count == 0 && randomServers.Count == 0)
                    break;

                for (int i = 0, serversCount = randomServers.Count; i < serversCount; i++)
                {
                    var s = randomServers[i];

                    // We create a task, for each server that checks for the version
                    var t = Task.Factory.StartNew(async (state) =>
                    {
                        var connServers = (List<Server>)state;

                        if (cts.IsCancellationRequested) return;

                        var electrum = new ElectrumClient(new List<Server>() { s });
                        var res = await electrum.ServerVersion(CLIENT_NAME, REQUESTED_VERSION);

                        Debug.WriteLine(
                            "Connected to: {0}:{1} => {2}",
                            s.Domain,
                            s.PrivatePort,
                            res
                        );

                        lock (_lock) connectedServers.Add(s);

                        // When we get NUMBER_OF_RECENT_SERVERS we get out
                        if (connServers.Count >= NUMBER_OF_RECENT_SERVERS) cts.Cancel();
                    }, connectedServers);

                    tasks.Add(t);
                }

                // Executute (usually) NUMBER_OF_RECENT_SERVERS tasks...
                Task.WaitAll(tasks.ToArray());

                // Get out once we got enough
                if (connectedServers.Count > NUMBER_OF_RECENT_SERVERS)
                    break;
            }

            // Sadly, none connected...
            if (connectedServers.Count == 0)
            {
                Debug.WriteLine("Cound not connect to any server...");

                return;
            }

            // Show warning over connected to less than the prefered amount
            if (connectedServers.Count < NUMBER_OF_RECENT_SERVERS)
                Debug.WriteLine("Conneted to too few servers {0}", connectedServers.Count);


            // Save our file now.
            lock (_lock)
                File.WriteAllText(
                    GetRecentlyConnectedServersFileName(network),
                    JsonConvert.SerializeObject(connectedServers, Formatting.Indented)
                );
        }

        public static string GetLocalConfigFilePath(params string[] fileNames)
        {
            return Path.Combine(
                Path.GetDirectoryName(
                    Assembly.GetCallingAssembly().Location
                ), string.Join(Path.DirectorySeparatorChar.ToString(), fileNames.ToArray())
            );
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

        static string GetRecentlyConnectedServersFileName(Network network)
        {
            return GetLocalConfigFilePath($"recent_servers_{network.Name.ToLower()}.json");
        }
    }
}
