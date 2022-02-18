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
using System.Collections.Generic;
using System.Reactive.Disposables;

using ReactiveUI;
using Terminal.Gui;

using Liviano.CLI.Gui.ViewModels;

namespace Liviano.CLI.Gui.Views
{
    public class HomeView : Window, IViewFor<HomeViewModel>
    {
        readonly CompositeDisposable disposable = new();

        FrameView menuFrameView;
        FrameView contentFrameView;
        ListView menuItemsListView;

        List<string> menuItemsList = new() { "Mempool Info", "Home", "Receive", "Send", "Settings" };

        public HomeViewModel ViewModel { get; set; }

        public HomeView(HomeViewModel viewModel) : base($"{Version.ToString()} ~~~ ESC to close ~~~")
        {
            ViewModel = viewModel;

            ColorScheme = Colors.TopLevel;

            SetupMainFrames();
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

            menuFrameView.Add(menuItemsListView);

            contentFrameView = new("Content")
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

        protected override void Dispose(bool disposing)
        {
            disposable.Dispose();
            base.Dispose(disposing);
        }

        object IViewFor.ViewModel { get => ViewModel; set => ViewModel = (HomeViewModel)value; }
    }
}
