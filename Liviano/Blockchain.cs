//
// Blockchain.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2020 HODL Wallet
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
using System.Collections.Generic;

using NBitcoin;

using Liviano.Extensions;
using Liviano.Interfaces;
using Liviano.Electrum;
using System.Threading.Tasks;

namespace Liviano
{
    public class Blockchain
    {
        const int HEADER_SIZE = 80;

        /// <summary>
        /// The height of the blockchain an int
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// The network the blockchain belongs to.
        /// </summary>
        public Network Network { get; set; }

        /// <summary>
        /// Array of checkpoints to start syncing the headers
        /// </summary>
        public ChainedBlock[] Checkpoints { get; set; }

        /// <summary>
        /// A list of ChainedBlocks representing the block headers
        /// </summary>
        public List<ChainedBlock> Headers { get; set; }

        /// <summary>
        /// Responsible of saving and loading
        /// </summary>
        public IBlockchainStorage BlockchainStorage { get; set; }

        /// <summary>
        /// Initializer
        /// </summary>
        /// <param name="network">Network to get everything from</param>
        public Blockchain(Network network, IBlockchainStorage storage)
        {
            Network ??= network;
            BlockchainStorage = storage;
            Headers ??= new List<ChainedBlock>();

            Checkpoints ??= Network.GetCheckpoints().ToArray();
            AddGenesisBlockHeader();
            Height = GetHeight();

            BlockchainStorage.Blockchain = this;
        }

        /// <summary>
        /// Saves block headers
        /// </summary>
        public void Save()
        {
            BlockchainStorage.Save();
        }

        /// <summary>
        /// Loads the block headers
        /// </summary>
        public void Load()
        {
            Headers.Clear();
            Height = -1;

            Headers = BlockchainStorage.Load();

            if (Headers.Count == 0) AddGenesisBlockHeader();

            Height = GetHeight();
        }

        /*public void DownloadHeadersParallel(ElectrumPool pool)*/
        /*{*/
            /*var unsortedHeaders = new List<ChainedBlock>();*/
            /*Parallel.ForEach(Checkpoints, async cp =>*/
            /*{*/
                /*var index = Array.IndexOf(Checkpoints, cp);*/
                /*var start = index == 0 ? 0 : cp.Height + 1;*/

                /*if (start == 0)*/
                /*{*/
                    /*var genesis = new ChainedBlock(Network.GetGenesis().Header, 0);*/

                    /*unsortedHeaders.Add(genesis);*/

                    /*start++;*/
                /*}*/

                /*var count = cp.Height;*/
                /*var current = start;*/
                /*Console.WriteLine($"current: {current} count: {count}");*/
                /*while (current < cp.Height)*/
                /*{*/
                    /*var res = await DownloadRequestUntilResult(pool, current, count);*/

                    /*count = res.Count;*/
                    /*var hex = res.Hex;*/
                    /*var max = res.Max;*/

                    /*var height = current;*/
                    /*for (int i = 0; i < hex.Length; i += HEADER_SIZE * 2)*/
                    /*{*/
                        /*var headerHex = new string(hex.ToList().Skip(i).Take(HEADER_SIZE * 2).ToArray());*/

                        /*var chainedBlock = new ChainedBlock(*/
                            /*BlockHeader.Parse(headerHex, Network),*/
                            /*height++*/
                        /*);*/

                        /*unsortedHeaders.Add(chainedBlock);*/
                        /*current = chainedBlock.Height;*/
                    /*}*/
                /*}*/
            /*});*/

            /*Headers = unsortedHeaders.OrderBy(cb => cb.Height).ToList();*/
            /*Height = GetHeight();*/
        /*}*/

        public void DownloadHeadersParallel(ElectrumPool pool)
        {
            var unsortedHeaders = new List<ChainedBlock>();
            foreach (var cp in Checkpoints)
            {
                var index = Array.IndexOf(Checkpoints, cp);
                var start = index == 0 ? 0 : cp.Height + 1;

                if (start == 0)
                {
                    var genesis = new ChainedBlock(Network.GetGenesis().Header, 0);

                    unsortedHeaders.Add(genesis);

                    start++;
                }

                var count = cp.Height;
                var current = start;
                Console.WriteLine($"current: {current} count: {count}");
                Console.WriteLine($"cp.height: {cp.Height}");
                while (current < cp.Height)
                {
                    var t = DownloadRequestUntilResult(pool, current, count);
                    t.Wait();

                    var res = t.Result;

                    count = res.Count;
                    var hex = res.Hex;
                    var max = res.Max;

                    var height = current;
                    for (int i = 0; i < hex.Length; i += HEADER_SIZE * 2)
                    {
                        var headerHex = new string(hex.ToList().Skip(i).Take(HEADER_SIZE * 2).ToArray());

                        var chainedBlock = new ChainedBlock(
                            BlockHeader.Parse(headerHex, Network),
                            height++
                        );

                        unsortedHeaders.Add(chainedBlock);
                        current = chainedBlock.Height;
                    }
                }
            }

            Headers = unsortedHeaders.OrderBy(cb => cb.Height).ToList();
            Height = GetHeight();
        }

        async Task<ElectrumClient.BlockchainBlockHeadersInnerResult> DownloadRequestUntilResult(ElectrumPool pool, int current, int count)
        {
            if (pool.ElectrumClient is null)
            {
                pool.FindConnectedServersUntilMinNumber().Wait();
            }

            try
            {
                return await pool.DownloadHeaders(current, count);
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.Message);

                await Task.Delay(1000);

                return await DownloadRequestUntilResult(pool, current, count);
            }
        }

        public async Task DownloadHeaders(ElectrumPool pool)
        {
            var unsortedHeaders = new List<ChainedBlock>();

            for (int i = 0; i <= Checkpoints.Length; i++)
            {
                var cp = Checkpoints[i];
                var index = Array.IndexOf(Checkpoints, cp);
                var start = index == 0 ? 0 : cp.Height + 1;

                if (start == 0)
                {
                    var genesis = new ChainedBlock(Network.GetGenesis().Header, 0);

                    unsortedHeaders.Add(genesis);

                    start++;
                }

                var count = cp.Height;
                var current = start;
                Console.WriteLine($"current: {current} count: {count}");
                while (current < cp.Height)
                {
                    var res = await DownloadRequestUntilResult(pool, current, count);

                    count = res.Count;
                    var hex = res.Hex;
                    var max = res.Max;

                    var height = current;
                    for (int j = 0; j < hex.Length; j += (HEADER_SIZE * 2))
                    {
                        var headerHex = new string(hex.ToList().Skip(j).Take(HEADER_SIZE * 2).ToArray());

                        var chainedBlock = new ChainedBlock(
                            BlockHeader.Parse(headerHex, Network),
                            height++
                        );

                        unsortedHeaders.Add(chainedBlock);
                        current = chainedBlock.Height;
                    }
                }
            }

            Headers = unsortedHeaders.OrderBy(cb => cb.Height).ToList();
            Height = GetHeight();
        }

        /// <summary>
        /// Gets the current height
        /// </summary>
        /// <returns>A <see cref="int"> with the height</returns>
        int GetHeight()
        {
            return Headers.Last().Height;
        }

        /// <summary>
        /// Adds the genesis block header use in case of empty
        /// </summary>
        void AddGenesisBlockHeader()
        {
            Headers.Insert(0, new ChainedBlock(Network.GetGenesis().Header, 0));
        }
    }
}
