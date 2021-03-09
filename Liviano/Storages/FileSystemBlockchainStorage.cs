//
// FileSystemBlockchainStorage.cs
//
// Author:
//       Igor Guerrero <igorgue@protonmail.com>
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
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;

//using NBitcoin;
//using NBitcoin.DataEncoders;

//using Liviano.Interfaces;
//using Liviano.Utilities;

//namespace Liviano.Storages
//{
    //public class FileSystemBlockchainStorage : IBlockchainStorage
    //{
        //const int HEADER_SIZE = 80;
        //public Blockchain Blockchain { get; set; }
        //public string RootDirectory { get; set; }

        //public FileSystemBlockchainStorage(string directory = "blockchain")
        //{
            //directory = Path.GetFullPath(directory);

            //if (!Directory.Exists(directory))
            //{
                //try
                //{
                    //Directory.CreateDirectory(directory);
                //}
                //catch (Exception ex)
                //{
                    //Debug.WriteLine($"[FileSystemBlockchainStorage] Error: {ex.Message}");
                //}
            //}

            //RootDirectory = directory;
        //}

        //public List<ChainedBlock> Load()
        //{
            //Guard.NotNull(Blockchain, nameof(Blockchain));
            //Guard.NotNull(RootDirectory, nameof(RootDirectory));

            //var headers = new List<ChainedBlock> ();
            //var fileName = GetFileName();

            //if (!File.Exists(fileName)) return headers;

            //var bytes = File.ReadAllBytes(fileName);

            //var height = 0;
            //for (int i = 0; i < bytes.Length; i += HEADER_SIZE)
            //{
                //var data = bytes.Skip(i).Take(HEADER_SIZE);
                //var hex = Encoders.Hex.EncodeData(data.ToArray());

                //var chainedBlock = new ChainedBlock(BlockHeader.Parse(hex, Blockchain.Network), height++);

                //headers.Add(chainedBlock);
            //}

            //return headers;
        //}

        //public void Save()
        //{
            //Guard.NotNull(Blockchain, nameof(Blockchain));
            //Guard.NotNull(RootDirectory, nameof(RootDirectory));

            //var fileName = GetFileName();

            //foreach (var bytes in Blockchain.Headers.Select(cb => cb.Header.ToBytes()))
            //{
                //File.WriteAllBytes(fileName, bytes);
            //}
        //}

        //string GetFileName()
        //{
            //var slash = Path.DirectorySeparatorChar;
            //var network = Blockchain.Network.ToString().ToLower();

            //return $"{RootDirectory}{slash}{network}_blockchain_headers";
        //}
    //}
//}
