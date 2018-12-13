using System;
using System.Collections.Generic;
using System.IO;
using NBitcoin;

namespace Liviano
{
    public static class WalletExtensions
    {
        /// <summary>
        /// Determines whether the chain is downloaded and up to date.
        /// </summary>
        /// <param name="chain">The chain.</param>
        public static bool IsDownloaded(this ConcurrentChain chain)
        {
            if (chain.Tip == null) return false;

            return chain.Tip.Header.BlockTime.ToUnixTimeSeconds() > (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - TimeSpan.FromHours(1).TotalSeconds);
        }

        /// <summary>
        /// Gets the height of the first block created at or after this date.
        /// </summary>
        /// <param name="chain">The chain of blocks.</param>
        /// <param name="date">The date.</param>
        /// <returns>The height of the first block created after the date.</returns>
        public static int GetHeightAtTime(this ConcurrentChain chain, DateTime date)
        {
            int blockSyncStart = 0;
            int upperLimit = chain.Tip.Height;
            int lowerLimit = 0;
            bool found = false;
            while (!found)
            {
                int check = lowerLimit + (upperLimit - lowerLimit) / 2;
                DateTime blockTimeAtCheck = chain.GetBlock(check).Header.BlockTime.DateTime;

                if (blockTimeAtCheck > date)
                {
                    upperLimit = check;
                }
                else if (blockTimeAtCheck < date)
                {
                    lowerLimit = check;
                }
                else
                {
                    return check;
                }

                if (upperLimit - lowerLimit <= 1)
                {
                    blockSyncStart = upperLimit;
                    found = true;
                }
            }

            return blockSyncStart;
        }

        public static IEnumerable<(int Height, BlockHeader BlockHeader)> GetCheckpoints(this Network network)
        {
            List<(int Height, BlockHeader BlockHeader)> checkpoints = new List<(int, BlockHeader)>();
            List<(int Height, int Version, uint256 PrevBlockHeaderHash, uint256 MerkleRootHash, uint Time, uint NBits, uint Nonce)> rawBlockHeaders = new List<(int, int, uint256, uint256, uint, uint, uint)>();

            if (network == Network.Main)
            {
                //{ 0, uint256("000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f"), 1231006505, 0x1d00ffff },
                //{ 20160, uint256("000000000f1aef56190aee63d33a373e6487132d522ff4cd98ccfc96566d461e"), 1248481816, 0x1d00ffff },
                //{ 40320, uint256("0000000045861e169b5a961b7034f8de9e98022e7a39100dde3ae3ea240d7245"), 1266191579, 0x1c654657 },
                //{ 60480, uint256("000000000632e22ce73ed38f46d5b408ff1cff2cc9e10daaf437dfd655153837"), 1276298786, 0x1c0eba64 },
                //{ 80640, uint256("0000000000307c80b87edf9f6a0697e2f01db67e518c8a4d6065d1d859a3a659"), 1284861847, 0x1b4766ed },
                //{ 100800, uint256("000000000000e383d43cc471c64a9a4a46794026989ef4ff9611d5acb704e47a"), 1294031411, 0x1b0404cb },
                //{ 120960, uint256("0000000000002c920cf7e4406b969ae9c807b5c4f271f490ca3de1b0770836fc"), 1304131980, 0x1b0098fa },
                //{ 141120, uint256("00000000000002d214e1af085eda0a780a8446698ab5c0128b6392e189886114"), 1313451894, 0x1a094a86 },
                //{ 161280, uint256("00000000000005911fe26209de7ff510a8306475b75ceffd434b68dc31943b99"), 1326047176, 0x1a0d69d7 },
                //{ 181440, uint256("00000000000000e527fc19df0992d58c12b98ef5a17544696bbba67812ef0e64"), 1337883029, 0x1a0a8b5f },
                //{ 201600, uint256("00000000000003a5e28bef30ad31f1f9be706e91ae9dda54179a95c9f9cd9ad0"), 1349226660, 0x1a057e08 },
                //{ 221760, uint256("00000000000000fc85dd77ea5ed6020f9e333589392560b40908d3264bd1f401"), 1361148470, 0x1a04985c },
                //{ 241920, uint256("00000000000000b79f259ad14635739aaf0cc48875874b6aeecc7308267b50fa"), 1371418654, 0x1a00de15 },
                //{ 262080, uint256("000000000000000aa77be1c33deac6b8d3b7b0757d02ce72fffddc768235d0e2"), 1381070552, 0x1916b0ca },
                //{ 282240, uint256("0000000000000000ef9ee7529607286669763763e0c46acfdefd8a2306de5ca8"), 1390570126, 0x1901f52c },
                //{ 302400, uint256("0000000000000000472132c4daaf358acaf461ff1c3e96577a74e5ebf91bb170"), 1400928750, 0x18692842 },
                //{ 322560, uint256("000000000000000002df2dd9d4fe0578392e519610e341dd09025469f101cfa1"), 1411680080, 0x181fb893 },
                //{ 342720, uint256("00000000000000000f9cfece8494800d3dcbf9583232825da640c8703bcd27e7"), 1423496415, 0x1818bb87 },
                //{ 362880, uint256("000000000000000014898b8e6538392702ffb9450f904c80ebf9d82b519a77d5"), 1435475246, 0x1816418e },
                //{ 383040, uint256("00000000000000000a974fa1a3f84055ad5ef0b2f96328bc96310ce83da801c9"), 1447236692, 0x1810b289 },
                //{ 403200, uint256("000000000000000000c4272a5c68b4f55e5af734e88ceab09abf73e9ac3b6d01"), 1458292068, 0x1806a4c3 },
                //{ 423360, uint256("000000000000000001630546cde8482cc183708f076a5e4d6f51cd24518e8f85"), 1470163842, 0x18057228 },
                //{ 443520, uint256("00000000000000000345d0c7890b2c81ab5139c6e83400e5bed00d23a1f8d239"), 1481765313, 0x18038b85 },
                //{ 463680, uint256("000000000000000000431a2f4619afe62357cd16589b638bb638f2992058d88e"), 1493259601, 0x18021b3e },
                //{ 483840, uint256("0000000000000000008e5d72027ef42ca050a0776b7184c96d0d4b300fa5da9e"), 1504704195, 0x1801310b },
                //{ 504000, uint256("0000000000000000006cd44d7a940c79f94c7c272d159ba19feb15891aa1ea54"), 1515827554, 0x177e578c },
                //{ 524160, uint256("00000000000000000009d1e9bee76d334347060c6a2985d6cbc5c22e48f14ed2"), 1527168053, 0x17415a49 },
                //{ 544320, uint256("0000000000000000000a5e9b5e4fbee51f3d53f31f40cd26b8e59ef86acb2ebd"), 1538639362, 0x1725c191 }

            }
            else if (network == Network.TestNet)
            {
                rawBlockHeaders.Add(
                    (0, 1, new uint256("0000000000000000000000000000000000000000000000000000000000000000"), new uint256("000000000933ea01ad0ee984209779baaec3ced90fa3f408719526f8d77f4943"), uint.Parse("1296688602"), uint.Parse("486604799"), uint.Parse("414098458"))
                );
                rawBlockHeaders.Add(
                    (100800, 2, new uint256("0000000000af10f3079b4989ac4ff0baaecab38220510cdae9672d6922e93919"), new uint256("0000000000a33112f86f3f7b0aa590cb4949b84c2d9c673e9e303257b3be9000"), uint.Parse("1376543922"), uint.Parse("469817607"), uint.Parse("3078589146"))
                );
                rawBlockHeaders.Add(
                    (201600, 2, new uint256("00000000014ca604d769d4b99fff03ae3ac84d1e8eb991c5dac7c3cd4d9e68ee"), new uint256("0000000000376bb71314321c45de3015fe958543afcbada242a3b1b072498e38"), uint.Parse("1393813869"), uint.Parse("459287232"), uint.Parse("2273280314"))
                );
                //rawBlockHeaders.Add(
                //    (302400, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (403200, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (504000, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (604800, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (705600, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (806400, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (907200, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (1008000, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (1108800, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (1209600, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                //rawBlockHeaders.Add(
                //    (1310400, 2, new uint256(""), new uint256(""), uint.Parse(""), uint.Parse(""), uint.Parse(""))
                //);
                rawBlockHeaders.Add(
                    (1411200, 2, new uint256("00000000000000a5bf9029aebb1956200304ffee31bc09f1323ae412d81fa2b2"), new uint256("00000000000000008b3baea0c3de24b9333c169e1543874f4202397f5b8502cb"), uint.Parse("1535560970"), uint.Parse("424329477"), uint.Parse("2681700833"))
                );
            }

            foreach (var rawBlockHeader in rawBlockHeaders)
            {
                var blockHeader = network.Consensus.ConsensusFactory.CreateBlockHeader();
                var bitcoinStream = new BitcoinStream(new MemoryStream(), serializing: true);

                bitcoinStream.ReadWrite(rawBlockHeader.Version);
                bitcoinStream.ReadWrite(rawBlockHeader.PrevBlockHeaderHash);
                bitcoinStream.ReadWrite(rawBlockHeader.MerkleRootHash);
                bitcoinStream.ReadWrite(rawBlockHeader.Time);
                bitcoinStream.ReadWrite(rawBlockHeader.Nonce);

                blockHeader.ReadWrite(bitcoinStream);

                //blockHeader.Version = rawBlockHeader.Version;
                //blockHeader.HashPrevBlock = rawBlockHeader.PrevBlockHeaderHash;
                //blockHeader.HashMerkleRoot = rawBlockHeader.MerkleRootHash;
                //blockHeader.BlockTime = DateTimeOffset.Parse(rawBlockHeader.Time.ToString());
                //blockHeader.Bits = new Target(rawBlockHeader.NBits);
                //blockHeader.Nonce = rawBlockHeader.Nonce;

                checkpoints.Add((rawBlockHeader.Height, blockHeader));
                //var blockHeader = new BlockHeader(bitcoinStream., _Network);
            }

            return checkpoints;
        }
    }
}