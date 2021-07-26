//
// IElectrumPool.cs
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
using System.Threading;
using System.Threading.Tasks;

using NBitcoin;

using Liviano.Events;
using Liviano.Models;
using Liviano.Electrum;

namespace Liviano.Interfaces
{
    public interface IElectrumPool
    {
        bool Connected { get; }
        bool IsPinging { get; }
        Server CurrentServer { get; set; }
        ElectrumClient ElectrumClient { get; set; }

        event EventHandler<Server> OnCurrentServerChangedEvent;
        event EventHandler<Server> OnConnected;
        event EventHandler<Server> OnDisconnectedEvent;

        event EventHandler OnDoneFindingPeersEvent;
        event EventHandler OnCancelFindingPeersEvent;

        event EventHandler<TxEventArgs> OnNewTransaction;
        event EventHandler<TxEventArgs> OnUpdateTransaction;

        event EventHandler OnSyncStarted;
        event EventHandler OnSyncFinished;
        event EventHandler OnWatchStarted;

        event EventHandler<WatchAddressEventArgs> OnWatchAddressNotified;
        event EventHandler<NewHeaderEventArgs> OnNewHeaderNotified;
        event EventHandler<NewHeaderEventArgs> OnUpdatedHeader;

        /// <summary>
        /// Broadcast Bitcoin Transaction
        /// </summary>
        /// <param name="transaction">A signed <see cref="Transaction"/> to be broadcasted</param>
        Task<(bool Result, string Error)> Broadcast(Transaction transaction);

        /// <summary>
        /// Susbscribes to the headers until tip
        /// </summary>
        /// <param name=""></param>
        Task SubscribeToHeaders(IWallet wallet, CancellationToken ct);

        Task DownloadHeaders(IWallet wallet, long fromHeight);

        /// <summary>
        /// Event handler when you get a connected server
        /// </summary>
        void HandleConnectedServers(object sender, EventArgs e);

        /// <summary>
        /// Connect async
        /// </summary>
        /// <param name="retries">A <see cref="int"/> to retry</param>
        /// <param name="cts">A cancellation tocken source</param>
        Task Connect(int retries, CancellationTokenSource cts = null);

        /// <summary>
        /// Get current server banner
        /// </summary>
        Task<string> Banner();

        /// <summary>
        /// Ping the current server
        /// </summary>
        Task<bool> Ping();

        /// <summary>
        /// Ping the current server periodically with a callback for failures
        /// </summary>
        Task PeriodicPing(Action<DateTimeOffset?> successCallback, Action<DateTimeOffset?> failedCallback);

        /// <summary>
        /// Sync wallet
        /// </summary>
        /// <param name="wallet">a <see cref="IWallet"/> to sync</param>
        /// <param name="cancellationToken">a <see cref="CancellationToken"/> to stop this</param>
        Task SyncWallet(IWallet wallet, CancellationToken ct);

        /// <summary>
        /// Sync a wallet account
        /// </summary>
        /// <param name="account">a <see cref="IAccount"/> to sync</param>
        /// <param name="cancellationToken">a <see cref="CancellationToken"/> to stop this</param>
        Task SyncAccount(IAccount account, CancellationToken ct);

        /// <summary>
        /// Watches a wallet for new transactions
        /// </summary>
        /// <param name="wallet">A <see cref="IWallet"/> to watch</param>
        /// <param name="ct">A <see cref="CancellationToken"/> to cancel</param>
        Task WatchWallet(IWallet wallet, CancellationToken ct);

        /// <summary>
        /// Watches a wallet's account for new transactions
        /// </summary>
        /// <param name="wallet">A <see cref="IWallet"/> to watch</param>
        /// <param name="ct">A <see cref="CancellationToken"/> to cancel</param>
        Task WatchAccount(IAccount account, CancellationToken ct);

        /// <summary>
        /// Loads the pool from the filesystem
        /// </summary>
        /// <param name="network">a <see cref="Network"/> to load files from</param>
        /// <param name="localDirectory">a <see cref="string"/> to servers file fromm (custom server)</param>
        /// <returns>A new <see cref="ElectrumPool"/></returns>
        IElectrumPool Load(Network network, string localDirectory);
    }
}
