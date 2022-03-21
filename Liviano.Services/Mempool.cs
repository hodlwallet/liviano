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
using System.Threading.Tasks;

using ReactiveUI;
using Refit;

using Liviano.Services.Interfaces;
using Liviano.Services.Models;

namespace Liviano.Services
{
    public class Mempool : ReactiveObject, IService
    {
        static IMempoolHttpService MempoolHttpService => CustomRestService.For<IMempoolHttpService>(Constants.MEMPOOL_SPACE_API);

        readonly CancellationTokenSource cts = new();

        MempoolStatisticEntity[] stats;
        public MempoolStatisticEntity[] Stats
        {
            get => stats;
            set => this.RaiseAndSetIfChanged(ref stats, value);
        }

        public Mempool() { }

        public async Task Update()
        {
            Debug.WriteLine("[Update] Update stats from mempool service");

            try
            {
                Stats = await MempoolHttpService.GetStatistics2h();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[Update] Error: {e}");
            }
        }

        public void Cancel()
        {
            Debug.WriteLine("[Cancel] Cancelling Mempool service");

            cts.Cancel();
        }

        public void Start()
        {
            Debug.WriteLine("[Start] Started Mempool service.");

            Stats = MempoolHttpService.GetStatistics2h().Result;

            Observable
                .Interval(TimeSpan.FromMilliseconds(Constants.MEMPOOL_SPACE_2H_STATS_INTERVAL_MS), RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await Update(), cts.Token);
        }

    }
}
