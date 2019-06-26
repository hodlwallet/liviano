using System;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using Xunit;

using NBitcoin;
using NBitcoin.DataEncoders;
using System.IO;

namespace Liviano.Tests.Liviano
{
    class BlockcypherBlock
    {
        public string hash { get; set; }
        public int height { get; set; }
        public string chain { get; set; }
        public int total { get; set; }
        public int fees { get; set; }
        public int size { get; set; }
        public int ver { get; set; }
        public DateTime time { get; set; }
        public DateTime received_time { get; set; }
        public string coinbase_addr { get; set; }
        public string relayed_by { get; set; }
        public int bits { get; set; }
        public long nonce { get; set; } // TODO Why is this a `long`?
        public int n_tx { get; set; }
        public string prev_block { get; set; }
        public string mrkl_root { get; set; }
        public List<string> txids { get; set; }
        public int depth { get; set; }
        public string prev_block_url { get; set; }
        public string tx_url { get; set; }

        public string GetBlockHeaderHex()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] versionBytes = BitConverter.GetBytes(ver);

                ms.Write(versionBytes);

                byte[] prevBlockBytes = new HexEncoder().DecodeData(prev_block);
                Array.Reverse(prevBlockBytes);

                ms.Write(prevBlockBytes);

                byte[] merkleRootBytes = new HexEncoder().DecodeData(mrkl_root);
                Array.Reverse(merkleRootBytes);

                ms.Write(merkleRootBytes);

                byte[] timestampBytes = BitConverter.GetBytes((int) new DateTimeOffset(time).ToUnixTimeSeconds());

                ms.Write(timestampBytes);

                byte[] bitsBytes = BitConverter.GetBytes(bits);

                ms.Write(bitsBytes);

                // FIXME What is this thing? A log or an int? We need 4 bytes not 8
                byte[] nonceBytes = BitConverter.GetBytes(nonce).Take(4).ToArray();
                // byte[] nonceBytes = BitConverter.GetBytes(nonce); // Thi should be the correct code...

                ms.Write(nonceBytes);

                return new HexEncoder().EncodeData(ms.ToArray());
            }
        }
}

    public class WalletExtensionsTest
    {
        const string BLOCKCYPHER_MAINNET_BLOCK_API = "https://api.blockcypher.com/v1/btc/main/blocks/{0}";

        const string BLOCKCYPHER_TESTNET_BLOCK_API = "https://api.blockcypher.com/v1/btc/test3/blocks/{0}";

        static HttpClient _HttpClient = new HttpClient();

        [Fact]
        public void NetworkCheckpointsValidationTest()
        {
            ValidateCheckpointsForNetwork(Network.Main);
            ValidateCheckpointsForNetwork(Network.TestNet);
        }

        async void ValidateCheckpointsForNetwork(Network network)
        {
            string explorerApi = network == Network.Main ? BLOCKCYPHER_MAINNET_BLOCK_API : BLOCKCYPHER_TESTNET_BLOCK_API;

            foreach (var checkpoint in network.GetCheckpoints())
            {
                string url = string.Format(explorerApi, checkpoint.Height);
                HttpResponseMessage response = await _HttpClient.GetAsync(url);

                // Test "pass" if the api doesn't work for us...
                if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

                if (!response.IsSuccessStatusCode)
                {
                    throw new ArgumentException($"Invalid api call {url}");
                }

                string content = await response.Content.ReadAsStringAsync();
                BlockcypherBlock block = Newtonsoft.Json.JsonConvert.DeserializeObject<BlockcypherBlock>(content);
                string checkpointBlockHeaderHex = new HexEncoder().EncodeData(checkpoint.Header.ToBytes());

                Assert.Equal(block.GetBlockHeaderHex(), checkpointBlockHeaderHex);

                // Blockcypher's api limits you to 200 req / hour, so count them...
                Thread.Sleep(1000);
            }
        }
    }
}