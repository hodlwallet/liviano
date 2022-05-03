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
using System.Diagnostics;
using System.Reactive.Disposables;

using Terminal.Gui;

using Liviano.Bips;
using Liviano.CLI.Gui.Interfaces;
using Liviano.CLI.Gui.ViewModels;
using Liviano.CLI.Gui.Views;
using Liviano.Exceptions;
using Liviano.Interfaces;
using Liviano.Storages;

namespace Liviano.CLI.Gui
{
    public class MainWindow : Window
    {
        readonly CompositeDisposable disposable = new();

        FrameView menuFrameView;
        FrameView contentFrameView;
        ListView menuItemsListView;

        readonly HomeView homeView;
        readonly MempoolGraphView mempoolGraphView;
        readonly FeeEstimatorView feeEstimatorView;
        readonly PriceView priceView;

        //readonly string[] menuItemsList = { "Home", "Receive", "Send", "Settings", "Mempool Info", "Mempool Graph" };
        readonly string[] menuItemsList = { "Home", "Mempool Graph", "Fee Estimator", "Price" };
        //readonly string[] menuItemsList = { "Mempool Graph", "Fee Estimator", "Price" };

        readonly IWallet wallet;

        public HomeViewModel ViewModel { get; set; }

        public MainWindow(Config config) : base($"{Version.ToString()} ~~~ ESC to close ~~~")
        {
            ColorScheme = Colors.TopLevel;
            wallet = Load(config);
            homeView = new HomeView(new HomeViewModel(wallet));
            mempoolGraphView = new MempoolGraphView(new MempoolGraphViewModel());
            feeEstimatorView = new FeeEstimatorView(new FeeEstimatorViewModel());
            priceView = new PriceView(new PriceViewModel());

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
                case "Fee Estimator":
                    AddContent(feeEstimatorView);
                    break;
                case "Price":
                    AddContent(priceView);
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

        static IWallet Load(Config config, string passphrase = null, bool skipAuth = false)
        {
            var network = Hd.GetNetwork(config.Network);

            var storage = new FileSystemWalletStorage(config.WalletId, network);

            if (!storage.Exists())
            {
                Debug.WriteLine($"[Load] Wallet {config.WalletId} doesn't exists. Make sure you're on the right network");

                throw new WalletException("Invalid wallet id");
            }

            return storage.Load(passphrase, out WalletException _, skipAuth);
        }

        protected override void Dispose(bool disposing)
        {
            disposable.Dispose();
            base.Dispose(disposing);
        }
    }
}
