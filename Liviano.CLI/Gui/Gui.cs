//
// Gui.cs
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

using ReactiveUI;

using Liviano.Services;
using Newtonsoft.Json;

namespace Liviano.CLI.Gui
{
    public static class Gui
    {
        public static void Run()
        {
            //Application.Init();

            //RxApp.MainThreadScheduler = TerminalScheduler.Default;
            //RxApp.TaskpoolScheduler = TaskPoolScheduler.Default;

            //Application.QuitKey = Key.Esc;
            //Application.Run(new MainWindow(Config.Load()));
            //Application.Shutdown();

            var service = new Price();
            service.Start();

            service.WhenAnyValue(s => s.Rates).Subscribe(x => Console.WriteLine(JsonConvert.SerializeObject(x, Formatting.Indented)));

            Console.WriteLine("Waiting for messages...");
            Console.ReadLine();
        }
    }
}
