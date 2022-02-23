//
// HomeViewModel.cs
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
//using System;
//using System.Reactive;
//using System.Reactive.Linq;
using System;
using System.Reactive.Linq;
using System.Runtime.Serialization;
//using System.Threading.Tasks;
using NStack;
using ReactiveUI;
//using ReactiveUI.Fody.Helpers;

namespace Liviano.CLI.Gui.ViewModels
{
    [DataContract]
    public class HomeViewModel : ReactiveObject
    {
        DateTimeOffset time;
        public DateTimeOffset Time
        {
            get => time;
            set => this.RaiseAndSetIfChanged(ref time, value);
        }

        readonly ObservableAsPropertyHelper<ustring> timeStr;
        public ustring TimeStr => timeStr.Value;

        public HomeViewModel()
        {
            timeStr = this
                .WhenAnyValue(x => x.Time)
                .WhereNotNull()
                .Select(x => (ustring)x.ToString())
                .ToProperty(this, nameof(TimeStr));

            Observable.Interval(TimeSpan.FromSeconds(1)).Subscribe(_ => Time = DateTime.UtcNow);
        }
    }
}
