//
// MempoolGraphViewModel.cs
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
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Serialization;

using ReactiveUI;

using Liviano.Services;
using Liviano.Services.Models;

namespace Liviano.CLI.Gui.ViewModels
{
    [DataContract]
    public class MempoolGraphViewModel : ReactiveObject
    {
        MempoolStatisticEntity stat;
        public MempoolStatisticEntity Stat
        {
            get => stat;
            set => this.RaiseAndSetIfChanged(ref stat, value);
        }

        readonly Mempool MempoolService = new();

        public MempoolGraphViewModel()
        {
            MempoolService.Start();
            Stat = MempoolService.Stats.First();

            MempoolService
                .WhenAnyValue(service => service.Stats)
                .Select(stats => stats.First())
                .BindTo(this, vm => vm.Stat);
        }
    }
}
