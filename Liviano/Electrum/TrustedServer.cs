//
// TrustedServer.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2021 HODL Wallet
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE
// OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using System.IO;

using NBitcoin;
using Newtonsoft.Json;

using Liviano.Models;
using Liviano.Interfaces;
using Liviano.Extensions;
using Liviano.Exceptions;
using Liviano.Events;

using static Liviano.Electrum.ElectrumClient;

namespace Liviano.Electrum
{
    public class TrustedServer : IElectrumPool
    {
        string host;
        int port;
        bool isSsl;

        public Server Server { get; set; }
        public ElectrumClient ElectrumClient { get; set; }

        public TrustedServer()
        {
        }
        

        public TrustedServer(string host, int port, bool isSsl = true)
        {
            this.host = host;
            this.port = port;
            this.isSsl = isSsl;
        }

        public async Task<bool> BroadcastTransaction(Transaction transaction)
        {
            var txHex = transaction.ToHex();

            try
            {
                var broadcast = await ElectrumClient.BlockchainTransactionBroadcast(txHex);

                if (broadcast.Result != transaction.GetHash().ToString())
                {
                    Debug.WriteLine("[BroadcastTransaction]  Error could not broadcast");

                    return false;
                }
            }
            catch (Exception err)
            {
                Debug.WriteLine($"[BroadcastTransaction]  Error could not broadcast: {err.Message}");

                return false;
            }


            return true;
        }

        public static IElectrumPool Load(Network network = null)
        {
            return new TrustedServer();
        }
    }
}
