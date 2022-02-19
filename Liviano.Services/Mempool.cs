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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Liviano.Services.Models;

using ReactiveUI;

using Refit;

namespace Liviano.Services
{
    public class Mempool : ReactiveObject, IService
    {
#if DEBUG
        const int MEMPOOL_SPACE_2H_STATS_DELAY = 1;
#else
        const int MEMPOOL_SPACE_2H_STATS_DELAY = 10_000;
#endif

        public IMempoolHttpService MempoolHttpService => RestService.For<IMempoolHttpService>(Constants.MEMPOOL_SPACE_2H_STATS);

        CancellationTokenSource cts = new();
        IObservable<long> obs;

        public ObservableAsPropertyHelper<List<MempoolStatisticEntity>> stats;
        public List<MempoolStatisticEntity> Stats => stats.Value;

        public void Start()
        {
            Debug.WriteLine("[Start] Started Mempool service.");

            //stats = this
                //.WhenAnyValue(x => x.Stats)
                //.Throttle(TimeSpan.FromSeconds(MEMPOOL_SPACE_2H_STATS_DELAY))
                //.Select(x => x)
                //.DistinctUntilChanged()
                //.Where(x => x is not null)
                //.SelectMany(GetStats)
                //.ObserveOn(RxApp.MainThreadScheduler)
                //.ToProperty(this, x => x.Stats);

            obs = Observable.Interval(TimeSpan.FromSeconds(1), Scheduler.Default);

            obs.Subscribe(_ => SetStats());

            //Task.Factory.StartNew(async (options) =>
            //{
                //Stats = MempoolHttpService.Get2hStatistics();

                //await Task.Delay(MEMPOOL_SPACE_2H_STATS_DELAY);
            //}, TaskCreationOptions.LongRunning, cts.Token);
        }

        public void Cancel()
        {
            cts.Cancel();
        }

        void SetStats()
        {
            Debug.WriteLine("[SetStats]");

            //stats = this.ThrownExceptions
            //return MempoolHttpService.Get2hStatistics();
        }

        List<MempoolStatisticEntity> GetStats(CancellationToken ct)
        {
            return MempoolHttpService.Get2hStatistics();
        }
    }
}
