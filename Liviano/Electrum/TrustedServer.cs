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

        public Server CurrentServer { get; set; }
        public ElectrumClient ElectrumClient { get; set; }
        public bool Connected { get; private set; }

        public event EventHandler<Server> OnCurrentServerChangedEvent;
        public event EventHandler<Server> OnConnected;
        public event EventHandler<Server> OnDisconnectedEvent;
        public event EventHandler OnDoneFindingPeersEvent;
        public event EventHandler OnCancelFindingPeersEvent;
        public event EventHandler<TxEventArgs> OnNewTransaction;
        public event EventHandler<TxEventArgs> OnUpdateTransaction;
        public event EventHandler OnSyncStarted;
        public event EventHandler OnSyncFinished;
        public event EventHandler OnWatchStarted;
        public event EventHandler<WatchAddressEventArgs> OnWatchAddressNotified;

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

        public async Task SyncWallet(IWallet wallet, CancellationToken ct)
        {
            await Task.Delay(1);
        }

        public void HandleConnectedServers(object sender, EventArgs e)
        {
        }

        public async Task FindConnectedServersUntilMinNumber(CancellationTokenSource cts = null)
        {
            await Task.Delay(1);
        }

        public async Task WatchWallet(IWallet wallet, CancellationToken ct)
        {
            await Task.Delay(1);
        }

        public async Task<BlockchainHeadersSubscribeInnerResult> SubscribeToHeaders()
        {
            var res = await ElectrumClient.BlockchainHeadersSubscribe();

            return res.Result;
        }
    }
}
