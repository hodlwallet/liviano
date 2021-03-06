//
// BlockcypherBlock.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2022 HODL Wallet
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
using System.IO;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

using Liviano.Extensions;

namespace Liviano.Tests.Funcional
{
    class BlockcypherBlock
    {
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(PropertyName = "chain")]
        public string Chain { get; set; }

        [JsonProperty(PropertyName = "total")]
        public long Total { get; set; }

        [JsonProperty(PropertyName = "fees")]
        public int Fees { get; set; }

        [JsonProperty(PropertyName = "size")]
        public int Size { get; set; }

        [JsonProperty(PropertyName = "ver")]
        public int Ver { get; set; }

        [JsonProperty(PropertyName = "time")]
        public DateTime Time { get; set; }

        [JsonProperty(PropertyName = "received_time")]
        public DateTime ReceivedTime { get; set; }

        [JsonProperty(PropertyName = "coinbase_addr")]
        public string CoinbaseAddr { get; set; }

        [JsonProperty(PropertyName = "relayed_by")]
        public string RelayedBy { get; set; }

        [JsonProperty(PropertyName = "bits")]
        public int Bits { get; set; }

        [JsonProperty(PropertyName = "nonce")]
        public long Nonce { get; set; }

        [JsonProperty(PropertyName = "n_tx")]
        public int NTx { get; set; }

        [JsonProperty(PropertyName = "prev_block")]
        public string PrevBlock { get; set; }

        [JsonProperty(PropertyName = "mrkl_root")]
        public string MrklRoot { get; set; }

        [JsonProperty(PropertyName = "txids")]
        public List<string> TxIds { get; set; }

        [JsonProperty(PropertyName = "depth")]
        public int Depth { get; set; }

        [JsonProperty(PropertyName = "prev_block_url")]
        public string PrevBlockUrl { get; set; }

        [JsonProperty(PropertyName = "tx_url")]
        public string TxUrl { get; set; }

        public string GetBlockHeaderHex()
        {
            using MemoryStream ms = new();
            byte[] versionBytes = BitConverter.GetBytes(Ver);

            ms.Write(versionBytes);

            byte[] prevBlockBytes = new HexEncoder().DecodeData(PrevBlock);
            Array.Reverse(prevBlockBytes);

            ms.Write(prevBlockBytes);

            byte[] merkleRootBytes = new HexEncoder().DecodeData(MrklRoot);
            Array.Reverse(merkleRootBytes);

            ms.Write(merkleRootBytes);

            byte[] timestampBytes = BitConverter.GetBytes((int)new DateTimeOffset(Time).ToUnixTimeSeconds());

            ms.Write(timestampBytes);

            byte[] bitsBytes = BitConverter.GetBytes(Bits);

            ms.Write(bitsBytes);

            byte[] nonceBytes = BitConverter.GetBytes(Nonce).Take(4).ToArray();

            ms.Write(nonceBytes);

            return new HexEncoder().EncodeData(ms.ToArray());
        }
    }

    public class WalletExtensionsTest
    {
        const string BLOCKCYPHER_MAINNET_BLOCK_API = "https://api.blockcypher.com/v1/btc/main/blocks/{0}";

        const string BLOCKCYPHER_TESTNET_BLOCK_API = "https://api.blockcypher.com/v1/btc/test3/blocks/{0}";

        static readonly HttpClient HttpClient = new();

        readonly ITestOutputHelper output;

        public WalletExtensionsTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NetworkCheckpointsValidationTest()
        {
            ValidateCheckpointsForNetwork(Network.Main);
            ValidateCheckpointsForNetwork(Network.TestNet);
        }

        void ValidateCheckpointsForNetwork(Network network)
        {
            string explorerApi = network == Network.Main ? BLOCKCYPHER_MAINNET_BLOCK_API : BLOCKCYPHER_TESTNET_BLOCK_API;


            foreach (var checkpoint in network.GetCheckpoints())
            {
                string url = string.Format(explorerApi, checkpoint.Height);
                string content = GetResponseFromUrlOrFile(url, checkpoint.Height, network).Result;

                BlockcypherBlock block = Newtonsoft.Json.JsonConvert.DeserializeObject<BlockcypherBlock>(content);
                string checkpointBlockHeaderHex = new HexEncoder().EncodeData(checkpoint.Header.ToBytes());

                Assert.Equal(block.GetBlockHeaderHex(), checkpointBlockHeaderHex);

                Thread.Sleep(100);
            }
        }

        async Task<string> GetResponseFromUrlOrFile(string url, int blockHeight, Network network)
        {
            string baseDirectory = Path.GetFullPath(
                Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), "../../../../tmp"
                )
            );
            string fileName = $"{baseDirectory}/{blockHeight}.{network.Name.ToLower()}.json";

            // Read the file if it exists and has content
            if (File.Exists(fileName) && new FileInfo(fileName).Length > 0)
                return await File.ReadAllTextAsync(fileName);

            // We now do the http request
            output.WriteLine($"File cache failed, calling url {url} to get data");

            // Blockcypher's api limits you to 200 req / hour, so count them...
            HttpResponseMessage response = await HttpClient.GetAsync(url);
            Thread.Sleep(1000); // And sleep for a second... first test run is slow but after is fast.

            if (response.StatusCode == HttpStatusCode.TooManyRequests || !response.IsSuccessStatusCode)
            {
                // NOTE Incase this fails read this...
                output.WriteLine("I've noticed that sometimes the API will return a 404, just rerun until you have content if that happens");

                throw new ArgumentException($"Invalid api call to {url}, Http error: {response.StatusCode}");
            }

            string content = await response.Content.ReadAsStringAsync();

            await File.WriteAllTextAsync(fileName, content);

            return content;
        }
    }
}
