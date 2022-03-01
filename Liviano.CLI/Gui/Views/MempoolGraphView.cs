//
// MempoolGraphView.cs
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
using System.Linq;
using System.Reactive.Linq;

using NStack;
using ReactiveUI;
using Terminal.Gui;
using Terminal.Gui.Graphs;

using Liviano.CLI.Gui.Interfaces;
using Liviano.CLI.Gui.ViewModels;
using Liviano.Services.Models;

namespace Liviano.CLI.Gui.Views
{
    public class MempoolGraphView : IView, IViewFor<MempoolGraphViewModel>
    {
        public string Title { get; } = "Mempool by vBytes (sat/vByte)";

        List<View> controls = new() { };
        public IEnumerable<View> Controls => controls;

        public MempoolGraphViewModel ViewModel { get; set; }
        object IViewFor.ViewModel { get => ViewModel; set => ViewModel = value as MempoolGraphViewModel; }

        Label time;
        GraphView graph;

        public MempoolGraphView(MempoolGraphViewModel viewModel)
        {
            ViewModel = viewModel;

            SetGui();

            this
                .WhenAnyValue(view => view.ViewModel.Stat)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(stat => SetStatToBars(stat));

            this
                .WhenAnyValue(view => view.ViewModel.Stat)
                .Select(
                    stat => ustring.Make($"Snapshot at: {DateTimeOffset.FromUnixTimeSeconds(stat.added).ToLocalTime()}")
                )
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(time, time => time.Text);
        }

        void SetGui()
        {
            time = new(ustring.Empty)
            {
                X = 0,
                Y = 0,
                TextAlignment = TextAlignment.Centered,
                Width = Dim.Fill(),
            };

            controls.Add(time);

            graph = new()
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
            };

            graph.Reset();

            var fg = Application.Driver.MakeAttribute(Colors.TopLevel.Normal.Foreground, Color.Black);
            var red = Application.Driver.MakeAttribute(Color.Red, Color.Black);

            graph.GraphColor = fg;

            var fill = new GraphCellToRender('\u2591');
            var series = new BarSeries()
            {
                Bars = new List<BarSeries.Bar>()
                {
                    new BarSeries.Bar("1-2", fill, 0f),
                    new BarSeries.Bar("2-3", fill, 0f),
                    new BarSeries.Bar("3-4", fill, 0f),
                    new BarSeries.Bar("4-5", fill, 0f),
                    new BarSeries.Bar("5-6", fill, 0f),
                    new BarSeries.Bar("6-8", fill, 0f),
                    new BarSeries.Bar("8-10", fill, 0f),
                    new BarSeries.Bar("10-12", fill, 0f),
                    new BarSeries.Bar("12-15", fill, 0f),
                    new BarSeries.Bar("15-20", fill, 0f),
                    new BarSeries.Bar("20-30", fill, 0f),
                    new BarSeries.Bar("30-40", fill, 0f),
                    new BarSeries.Bar("40-50", fill, 0f),
                    new BarSeries.Bar("50-60", fill, 0f),
                    new BarSeries.Bar("60-70", fill, 0f),
                    new BarSeries.Bar("70-80", fill, 0f),
                    new BarSeries.Bar("80-90", fill, 0f),
                    new BarSeries.Bar("90-100", fill, 0f),
                    new BarSeries.Bar("100-125", fill, 0f),
                    new BarSeries.Bar("125-150", fill, 0f),
                    new BarSeries.Bar("150-175", fill, 0f),
                    new BarSeries.Bar("175-200", fill, 0f),
                    new BarSeries.Bar("200-250", fill, 0),
                    new BarSeries.Bar("250-300", fill, 0f),
                    new BarSeries.Bar("300-350", fill, 0f),
                    new BarSeries.Bar("350-400", fill, 0f),
                    new BarSeries.Bar("400-500", fill, 0f),
                }
            };

            graph.Series.Add(series);

            series.Orientation = Orientation.Vertical;

            graph.CellSize = new PointF(0.1f, 0.25f);

            graph.AxisX.Increment = 0f;
            graph.AxisX.Minimum = 0;
            graph.AxisX.Text = "Fee Rate";

            graph.AxisY.Increment = 0.01f;
            graph.AxisY.ShowLabelsEvery = 1;
            graph.AxisY.LabelGetter = v => (v.Value / 10f).ToString("N2");
            graph.AxisY.Minimum = 0;
            graph.AxisY.Text = "MvB";

            graph.MarginBottom = 2;
            graph.MarginLeft = 6;

            graph.ScrollOffset = new PointF(0, 0);

            graph.SetNeedsDisplay();

            controls.Add(graph);
        }

        void SetStatToBars(MempoolStatisticEntity stat)
        {
            var fill = new GraphCellToRender('\u2591');
            var series = graph.Series[0] as BarSeries;

            for (int i = 0; i < 27; i++)
            {
                var barText = series.Bars[i].Text;

                series.Bars[i] = new BarSeries.Bar(barText, fill, stat.vsizes[i] / 100_000f);
            }

            graph.SetNeedsDisplay();
        }
    }
}
