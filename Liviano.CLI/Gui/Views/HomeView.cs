//
// HomeView.cs
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
using System.Reactive.Disposables;
using System.Reactive.Linq;

using Newtonsoft.Json;
using NStack;
using ReactiveUI;
using Terminal.Gui;
using Terminal.Gui.Graphs;

using Liviano.CLI.Gui.ViewModels;
using Liviano.Services.Models;

namespace Liviano.CLI.Gui.Views
{
    public class HomeView : Window, IViewFor<HomeViewModel>
    {
        readonly CompositeDisposable disposable = new();

        FrameView menuFrameView;
        FrameView contentFrameView;
        ListView menuItemsListView;
        Label mempoolView;
        GraphView mempoolGraphView;
        Label clockView;
        //readonly string[] menuItemsList = { "Home", "Receive", "Send", "Settings", "Mempool Info", "Mempool Graph", "Clock" };
        readonly string[] menuItemsList = { "Mempool Graph" };

        public HomeViewModel ViewModel { get; set; }

        public ClockViewModel ClockViewModel { get; set; }

        public HomeView(HomeViewModel viewModel) : base($"{Version.ToString()} ~~~ ESC to close ~~~")
        {
            ViewModel = viewModel;
            ClockViewModel ??= new();
            ColorScheme = Colors.TopLevel;

            SetGui();

            this
                .WhenAnyValue(x => x.ViewModel.Stat)
                .Select(x => (ustring)JsonConvert.SerializeObject(x, Formatting.Indented))
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(mempoolView, v => v.Text);

            this
                .WhenAnyValue(x => x.ViewModel.Stat)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => SetStatToBars(x));

            this
                .WhenAnyValue(x => x.ClockViewModel.Time)
                .Select(x => (ustring)x.ToString())
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(clockView, v => v.Text);
        }

        void SetStatToBars(MempoolStatisticEntity stat)
        {
            var fill = new GraphCellToRender('\u2591');

            for (int i = 0; i < 27; i++)
            {
                var barText = (mempoolGraphView.Series[0] as BarSeries).Bars[i].Text;

                (mempoolGraphView.Series[0] as BarSeries).Bars[i] = new BarSeries.Bar(barText, fill, stat.vsizes[i] / 100_000f);
            }

            mempoolGraphView.SetNeedsDisplay();
        }

        void SetGui()
        {
            menuFrameView = new("Menu")
            {
                X = 0,
                Y = 1,
                Width = 25,
                Height = Dim.Fill(1),
                CanFocus = true,
                Shortcut = Key.CtrlMask | Key.M,
            };

            menuItemsListView = new(menuItemsList)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(0),
                Height = Dim.Fill(0),
                AllowsMarking = false,
                CanFocus = true
            };

            menuItemsListView.OpenSelectedItem += _ => contentFrameView.SetFocus();
            menuItemsListView.SelectedItemChanged += MenuItemsListView_SelectedItemChanged;

            menuFrameView.Add(menuItemsListView);

            contentFrameView = new(menuItemsList[0])
            {
                X = 25,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
                CanFocus = true,
                Shortcut = Key.CtrlMask | Key.S,
            };

            Add(menuFrameView);
            Add(contentFrameView);

            // Main views...
            mempoolView = new Label("Mempool");
            clockView = new Label("Clock");
            mempoolGraphView = new GraphView()
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
            };

            SetGraphView();
        }

        void SetGraphView()
        {
            mempoolGraphView.Reset();

            var fg = Application.Driver.MakeAttribute(this.ColorScheme.Normal.Foreground, Color.Black);
            var red = Application.Driver.MakeAttribute(Color.Red, Color.Black);

            mempoolGraphView.GraphColor = fg;

            // The "LIGHT SHADE" unicode char
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

            mempoolGraphView.Series.Add(series);

            series.Orientation = Orientation.Vertical;

            // How much graph space each cell of the console depicts
            mempoolGraphView.CellSize = new PointF(0.1f, 0.25f);

            // No axis marks since Bar will add it's own categorical marks
            mempoolGraphView.AxisX.Increment = 0f;
            mempoolGraphView.AxisX.Minimum = 0;
            mempoolGraphView.AxisX.Text = "Fee Rate";

            mempoolGraphView.AxisY.Increment = 0.01f;
            mempoolGraphView.AxisY.ShowLabelsEvery = 1;
            mempoolGraphView.AxisY.LabelGetter = v => (v.Value / 10f).ToString("N2");
            mempoolGraphView.AxisY.Minimum = 0;
            mempoolGraphView.AxisY.Text = "MvB";

            // leave space for axis labels and title
            mempoolGraphView.MarginBottom = 2;
            mempoolGraphView.MarginLeft = 6;

            // Start the graph at 80 years because that is where most of our data is
            mempoolGraphView.ScrollOffset = new PointF(0, 0);

            mempoolGraphView.SetNeedsDisplay();
        }

        void MenuItemsListView_SelectedItemChanged(ListViewItemEventArgs args)
        {
            var name = menuItemsList[args.Item];
            contentFrameView.Title = name;

            contentFrameView.RemoveAll();

            switch (name)
            {
                case "Mempool Info":
                    contentFrameView.Add(mempoolView);
                    break;
                case "Mempool Graph":
                    contentFrameView.Add(mempoolGraphView);
                    break;
                case "Clock":
                    contentFrameView.Add(clockView);
                    break;
                default:
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            disposable.Dispose();
            base.Dispose(disposing);
        }

        object IViewFor.ViewModel { get => ViewModel; set => ViewModel = (HomeViewModel)value; }
    }
}
