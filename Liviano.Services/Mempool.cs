﻿//
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Refit;

using Liviano.Services.Models;

namespace Liviano.Services
{
    public class Mempool : IService
    {
        const int MEMPOOL_SPACE_2H_STATS_DELAY = 10_000;

        public IMempoolHttpService MempoolHttpService => RestService.For<IMempoolHttpService>(Constants.MEMPOOL_SPACE_2H_STATS);

        CancellationTokenSource cts = new();

        public List<MempoolStatisticEntity> Stats { get; set; } = new();

        public void Start()
        {
            Debug.WriteLine("[Start] Started");

            Task.Factory.StartNew(async (options) =>
            {
                Stats = MempoolHttpService.Get2hStatistics();

                await Task.Delay(MEMPOOL_SPACE_2H_STATS_DELAY);
            }, TaskCreationOptions.LongRunning, cts.Token);
        }

        public void Cancel()
        {
            cts.Cancel();
        }
    }
}
