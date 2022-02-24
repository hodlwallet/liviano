//
// TerminalScheduler.cs
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
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using Terminal.Gui;

namespace Liviano.CLI.Gui
{
    public class TerminalScheduler : LocalScheduler
    {
        public static readonly TerminalScheduler Default = new();
        TerminalScheduler() { }

        public override IDisposable Schedule<TState>(
            TState state,
            TimeSpan dueTime,
            Func<IScheduler, TState, IDisposable> action)
        {

            IDisposable PostOnMainLoop()
            {
                var composite = new CompositeDisposable(2);
                var cancellation = new CancellationDisposable();

                Application.MainLoop.Invoke(() =>
                {
                    if (!cancellation.Token.IsCancellationRequested)
                        composite.Add(action(this, state));
                });
                composite.Add(cancellation);

                return composite;
            }

            IDisposable PostOnMainLoopAsTimeout()
            {
                var composite = new CompositeDisposable(2);
                var timeout = Application.MainLoop.AddTimeout(dueTime, args =>
                {
                    composite.Add(action(this, state));
                    return false;
                });

                composite.Add(Disposable.Create(() => Application.MainLoop.RemoveTimeout(timeout)));

                return composite;
            }

            return dueTime == TimeSpan.Zero
                ? PostOnMainLoop()
                : PostOnMainLoopAsTimeout();
        }
    }
}
