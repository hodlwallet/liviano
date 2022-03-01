//
// MainWindow.cs
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

using Terminal.Gui;

using Liviano.CLI.Gui.ViewModels;
using Liviano.CLI.Gui.Views;
using Liviano.CLI.Gui.Interfaces;

namespace Liviano.CLI.Gui
{
    public class MainWindow : Window
    {
        readonly CompositeDisposable disposable = new();

        FrameView menuFrameView;
        FrameView contentFrameView;
        ListView menuItemsListView;

        HomeView homeView;
        MempoolGraphView mempoolGraphView;

        //readonly string[] menuItemsList = { "Home", "Receive", "Send", "Settings", "Mempool Info", "Mempool Graph" };
        readonly string[] menuItemsList = { "Mempool Graph", "Home" };

        public HomeViewModel ViewModel { get; set; }

        public MainWindow() : base($"{Version.ToString()} ~~~ ESC to close ~~~")
        {
            ColorScheme = Colors.TopLevel;
            homeView = new HomeView(new HomeViewModel());
            mempoolGraphView = new MempoolGraphView(new MempoolGraphViewModel());

            SetGui();
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
        }

        void MenuItemsListView_SelectedItemChanged(ListViewItemEventArgs args)
        {
            var name = menuItemsList[args.Item];

            contentFrameView.RemoveAll();
            switch (name)
            {
                case "Home":
                    AddContent(homeView);
                    break;
                case "Mempool Graph":
                    AddContent(mempoolGraphView);
                    break;
                default:
                    break;
            }
        }

        void AddContent(IView view)
        {
            contentFrameView.Title = view.Title;

            foreach (var control in view.Controls)
                contentFrameView.Add(control);
        }

        protected override void Dispose(bool disposing)
        {
            disposable.Dispose();
            base.Dispose(disposing);
        }
    }
}
