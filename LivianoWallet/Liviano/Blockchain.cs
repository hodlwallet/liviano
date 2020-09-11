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
            Headers ??= new List<ChainedBlock> ();

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

        public async Task DownloadHeaders(ElectrumPool pool)
        {
            int count = 0;
            int max = 0;
            string hex = "";

            while (true)
            {
                var cp = GetNextCheckpoint(Height);

                if (cp is null && count == 0) break;

                int blockHeadersToRequest;

                if (max != 0) blockHeadersToRequest = max;
                else blockHeadersToRequest = cp.Height;

                var res = await pool.DownloadHeaders(Height, blockHeadersToRequest);

                count = res.Count;
                hex = res.Hex;
                max = res.Max;

                for (int i = 0; i < hex.Length; i += (HEADER_SIZE * 2))
                {
                    var headerHex = new string(hex.ToList().Skip(i).Take(HEADER_SIZE * 2).ToArray());

                    var chainedBlock = new ChainedBlock(
                        BlockHeader.Parse(headerHex, Network),
                        Height + 1
                    );

                    Headers.Insert(Height + 1, chainedBlock);
                    Height = GetHeight();

                    Debug.WriteLine($"[DownloadHeaders] Current Height: {Height}");
                    Debug.WriteLine($"[DownloadHeaders] Headers Count: {Headers.Count}");
                }

                if (count == 0) break;
            }
        }

        ChainedBlock GetNextCheckpoint(int height)
        {
            foreach (var checkpoint in Checkpoints)
            {
                if (checkpoint.Height < height) continue;

                return checkpoint;
            }

            return null;
        }

        /// <summary>
        /// Gets the current height
        /// </summary>
        /// <returns>A <see cref="int"> with the height</returns>
        int GetHeight()
        {
            return Headers[Headers.Count - 1].Height;
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
