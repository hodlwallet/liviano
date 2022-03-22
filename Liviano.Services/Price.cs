//
// Price.cs
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
using System.Threading;
using System.Reactive.Linq;
using System.Threading.Tasks;

using ReactiveUI;

using Liviano.Services.Interfaces;
using Liviano.Services.Models;

namespace Liviano.Services
{
    public class Price : ReactiveObject, IService
    {
        static IPrecioHttpService PrecioHttpService => CustomRestService.For<IPrecioHttpService>(Constants.PRECIO_API);

        readonly CancellationTokenSource cts = new();

        PrecioEntity precio;
        public PrecioEntity Precio
        {
            get => precio;
            set => this.RaiseAndSetIfChanged(ref precio, value);
        }

        public void Cancel()
        {
            Debug.WriteLine("[Cancel] Price service cancelled.");

            cts.Cancel();
        }

        public void Start()
        {
            Debug.WriteLine("[Start] Started Price service.");

            Precio = PrecioHttpService.GetPrecio().Result;

            Observable
                .Interval(TimeSpan.FromMilliseconds(Constants.PRECIO_INTERVAL_MS), RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await Update(), cts.Token);
        }

        public async Task Update()
        {
            Debug.WriteLine("[Update] Update precio from price service");

            try
            {
                Precio = await PrecioHttpService.GetPrecio();

                //if (res.T != Precio.T) Precio = res;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[Update] Failed to get precio, error: {e}");
            }
        }
    }
}
