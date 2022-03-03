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

        Label fastestLabel;
        Label fastestValue;

        Label normalLabel;
        Label normalValue;

        Label slowestLabel;
        Label slowestValue;

        public FeeEstimatorView(FeeEstimatorViewModel viewModel)
        {
            ViewModel = viewModel;

            SetGui();

            this
                .WhenAnyValue(view => view.ViewModel.Fees)
                .Subscribe(_ => UpdateValues());
        }

        void UpdateValues()
        {
            fastestValue.Text = $"{ViewModel.Fees.fastest_sat_per_kilobyte / 1_000} sats per byte";
            normalValue.Text = $"{ViewModel.Fees.normal_sat_per_kilobyte / 1_000} sats per byte";
            slowestValue.Text = $"{ViewModel.Fees.slow_sat_per_kilobyte / 1_000} sats per byte";
        }

        void SetGui()
        {
            fastestLabel = new("Fastest fee:")
            {
                X = 0,
                Y = 0,
                Width = Dim.Percent(50f),
            };
            fastestValue = new()
            {
                X = Pos.AnchorEnd(),
                Y = 0,
            };

            controls.Add(fastestLabel);
            controls.Add(fastestValue);

            normalLabel = new("Normal fee:")
            {
                X = 0,
                Y = 2,
                Width = Dim.Percent(50f),
            };
            normalValue = new()
            {
                X = Pos.AnchorEnd(),
                Y = 2,
            };

            controls.Add(normalLabel);
            controls.Add(normalValue);

            slowestLabel = new("Slowest fee:")
            {
                X = 0,
                Y = 4,
                Width = Dim.Percent(50f),
            };
            slowestValue = new()
            {
                X = Pos.AnchorEnd(),
                Y = 4,
            };

            controls.Add(slowestLabel);
            controls.Add(slowestValue);
        }
    }
}
