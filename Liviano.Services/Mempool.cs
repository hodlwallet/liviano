//
// Mempool.cs
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
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;

using ReactiveUI;
using Refit;

using Liviano.Services.Models;
using System.Threading.Tasks;

namespace Liviano.Services
{
    public class Mempool : ReactiveObject, IService
    {
#if DEBUG
        const int MEMPOOL_SPACE_2H_STATS_INTERVAL_MS = 1000;
#else
        const int MEMPOOL_SPACE_2H_STATS_INTERVAL_MS = 10_000;
#endif

        static IMempoolHttpService MempoolHttpService => RestService.For<IMempoolHttpService>(Constants.MEMPOOL_SPACE_2H_STATS);

        readonly CancellationTokenSource cts = new();

        MempoolStatisticEntity[] stats;
        public MempoolStatisticEntity[] Stats
        {
            get => stats;
            set => this.RaiseAndSetIfChanged(ref stats, value);
        }

        public Mempool() { }

        public void Start()
        {
            Debug.WriteLine("[Start] Started Mempool service.");
            Stats = MempoolHttpService.GetStatistics2h().Result;

            Observable
                .Interval(TimeSpan.FromMilliseconds(MEMPOOL_SPACE_2H_STATS_INTERVAL_MS), RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await SetStats(), cts.Token);
        }

        async Task SetStats()
        {
            Debug.WriteLine("[SetStats] Setting stats from mempool service");

            Stats = await MempoolHttpService.GetStatistics2h();
        }

        public void Cancel()
        {
            Debug.WriteLine("[Cancel] Cancelling Mempool service");

            cts.Cancel();
        }
    }
}