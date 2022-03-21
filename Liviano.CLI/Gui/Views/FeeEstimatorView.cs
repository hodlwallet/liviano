//
// FeeEstimatorView.cs
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
    public class FeeEstimatorView : IView, IViewFor<FeeEstimatorViewModel>
    {
        public string Title { get; } = "Fee Estimator";

        public FeeEstimatorViewModel ViewModel { get; set; }
        object IViewFor.ViewModel { get => ViewModel; set => ViewModel = value as FeeEstimatorViewModel; }

        List<View> controls = new() { };
        public IEnumerable<View> Controls => controls;

        Label snapshotTime;

        Label fastestLabel;
        Label fastestRate;
        Label fastestTime;

        Label normalLabel;
        Label normalRate;
        Label normalTime;

        Label slowestLabel;
        Label slowestRate;
        Label slowestTime;

        public FeeEstimatorView(FeeEstimatorViewModel viewModel)
        {
            ViewModel = viewModel;

            SetGui();

            this
                .WhenAnyValue(view => view.ViewModel.Fees)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateValues());
        }

        void UpdateValues()
        {
            snapshotTime.Text = $"Snapshot at: {DateTimeOffset.UtcNow.ToLocalTime()}";

            fastestRate.Text = $"{ViewModel.Fees.FastestSatPerKilobyte / 1_000} sats per byte";
            fastestTime.Text = $"{ViewModel.Fees.FastestTime / 60} hours";

            normalRate.Text = $"{ViewModel.Fees.NormalSatPerKilobyte / 1_000} sats per byte";
            normalTime.Text = $"{ViewModel.Fees.NormalTime / 60} hours";

            slowestRate.Text = $"{ViewModel.Fees.SlowSatPerKilobyte / 1_000} sats per byte";
            slowestTime.Text = $"{ViewModel.Fees.SlowTime / 60} hours";
        }

        void SetGui()
        {
            snapshotTime = new("")
            {
                X = Pos.Center(),
                Y = 0,
            };

            controls.Add(snapshotTime);

            fastestLabel = new("Fastest fee:")
            {
                X = 0,
                Y = 2,
                Width = Dim.Percent(33.33f),
            };
            fastestRate = new()
            {
                X = Pos.Right(fastestLabel),
                Y = 2,
                Width = Dim.Percent(33.33f),
            };
            fastestTime = new()
            {
                X = Pos.Right(fastestRate),
                Y = 2,
                Width = Dim.Percent(33.33f),
            };

            controls.Add(fastestLabel);
            controls.Add(fastestRate);
            controls.Add(fastestTime);

            normalLabel = new("Normal fee:")
            {
                X = 0,
                Y = 4,
                Width = Dim.Percent(33.33f),
            };
            normalRate = new()
            {
                X = Pos.Right(normalLabel),
                Y = 4,
                Width = Dim.Percent(33.33f),
            };
            normalTime = new()
            {
                X = Pos.Right(normalRate),
                Y = 4,
                Width = Dim.Percent(33.33f),
            };

            controls.Add(normalLabel);
            controls.Add(normalRate);
            controls.Add(normalTime);

            slowestLabel = new("Slowest fee:")
            {
                X = 0,
                Y = 6,
                Width = Dim.Percent(33.33f),
            };
            slowestRate = new()
            {
                X = Pos.Right(slowestLabel),
                Y = 6,
                Width = Dim.Percent(33.33f),
            };
            slowestTime = new()
            {
                X = Pos.Right(slowestRate),
                Y = 6,
                Width = Dim.Percent(33.33f),
            };

            controls.Add(slowestLabel);
            controls.Add(slowestRate);
            controls.Add(slowestTime);
        }
    }
}
