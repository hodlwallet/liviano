//
// PriceView.cs
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
using System.Reactive.Linq;

using ReactiveUI;
using Terminal.Gui;

using Liviano.CLI.Gui.Interfaces;
using Liviano.CLI.Gui.ViewModels;

namespace Liviano.CLI.Gui.Views
{
    internal class PriceView : IView, IViewFor<PriceViewModel>
    {
        public string Title => "Price";

        public IEnumerable<View> Controls { get; } = new List<View>();

        public PriceViewModel ViewModel { get; set; }

        object IViewFor.ViewModel { get => ViewModel; set => ViewModel = value as PriceViewModel; }

        Label price;
        Label snapshotTime;

        FrameView ratesFrame;

        public PriceView(PriceViewModel viewModel)
        {
            ViewModel = viewModel;

            SetGui();

            this
                .WhenAnyValue(view => view.ViewModel.Precio)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdatePrecioValues());

            this
                .WhenAnyValue(view => view.ViewModel.Rates)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateRatesValues());
        }

        void SetGui()
        {
            snapshotTime = new("")
            {
                X = Pos.Center(),
                Y = 0,
            };

            (Controls as List<View>).Add(snapshotTime);

            price = new()
            {
                Text = "1 BTC ~= $...",
                X = Pos.Center(),
                Y = Pos.Center(),
                TextAlignment = TextAlignment.Centered,
            };

            (Controls as List<View>).Add(price);

            ratesFrame = new()
            {
                X = Pos.Center(),
                Y = Pos.Bottom(price),
            };
            (Controls as List<View>).Add(ratesFrame);
        }

        void UpdatePrecioValues()
        {
            snapshotTime.Text = $"Snapshot at: {DateTimeOffset.FromUnixTimeSeconds(ViewModel.Precio.T).ToLocalTime()}";
            price.Text = GetPriceStr();
        }

        void UpdateRatesValues()
        {
            snapshotTime.Text = $"Snapshot at: {DateTimeOffset.FromUnixTimeSeconds(ViewModel.Precio.T).ToLocalTime()}";
            //price.Text = GetPriceStr();
        }

        string GetPriceStr()
        {
            if (ViewModel.Precio is null) return $"1 BTC ~= $...";

            return $"1 BTC ~= ${ViewModel.Precio.CRaw:c}";
        }
    }
}