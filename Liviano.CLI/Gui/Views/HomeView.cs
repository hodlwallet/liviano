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

using Terminal.Gui;
using ReactiveUI;

using Liviano.CLI.Gui.Interfaces;
using Liviano.CLI.Gui.ViewModels;

namespace Liviano.CLI.Gui.Views
{
    public class HomeView : IView, IViewFor<HomeViewModel>
    {
        public string Title { get; } = "Home";

        public HomeViewModel ViewModel { get; set; }
        object IViewFor.ViewModel { get => ViewModel; set => ViewModel = value as HomeViewModel; }

        List<View> controls = new() { };
        public IEnumerable<View> Controls => controls;

        ListView transactions;
        Label balance;
        Label status;
        Label transactionsTitle;

        public HomeView(HomeViewModel viewModel)
        {
            ViewModel = viewModel;

            SetGui();
        }

        void SetGui()
        {
            transactions = new(new string[] { "Received 0.00084420 BTC at:tb1q053ptqlv0ugz8fcc3njw355rdluk4tqnhf0g0j 27/02/22 4:20pm" })
            {
                X = 0,
                Y = 4,
                Width = Dim.Fill(0),
                Height = Dim.Fill(0),
                CanFocus = true,
            };

            controls.Add(transactions);

            transactionsTitle = new("Transactions:")
            {
                X = 0,
                Y = 2,
                Width = Dim.Fill(0),
            };

            controls.Add(transactionsTitle);

            balance = new("Balance: 0.0 BTC")
            {
                X = 0,
                Y = 0,
                Width = Dim.Percent(95f),
            };

            controls.Add(balance);

            status = new("[Syncing]")
            {
                X = Pos.Right(balance),
                Y = 0,
            };

            controls.Add(status);
        }
    }
}
