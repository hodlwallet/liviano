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
using System.Reactive.Disposables;
using System.Reactive.Linq;

using ReactiveUI;
using Terminal.Gui;
using NStack;
using Newtonsoft.Json;

using Liviano.CLI.Gui.ViewModels;

namespace Liviano.CLI.Gui.Views
{
    public class HomeView : Window, IViewFor<HomeViewModel>
    {
        readonly CompositeDisposable disposable = new();

        FrameView menuFrameView;
        FrameView contentFrameView;
        ListView menuItemsListView;
        Label mempoolView;
        Label clockView;
        readonly string[] menuItemsList = { "Home", "Receive", "Send", "Settings", "Mempool Info", "Clock" };

        public HomeViewModel ViewModel { get; set; }

        public ClockViewModel ClockViewModel { get; set; }

        public HomeView(HomeViewModel viewModel) : base($"{Version.ToString()} ~~~ ESC to close ~~~")
        {
            ViewModel = viewModel;
            ClockViewModel ??= new();
            ColorScheme = Colors.TopLevel;

            SetupMainFrames();

            this
                .WhenAnyValue(x => x.ViewModel.Stats)
                .Select(x => (ustring)JsonConvert.SerializeObject(x))
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(mempoolView, v => v.Text);

            this
                .WhenAnyValue(x => x.ClockViewModel.Time)
                .Select(x => (ustring)x.ToString())
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(clockView, v => v.Text);
        }

        void SetupMainFrames()
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
