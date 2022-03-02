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
using System.Reactive.Linq;

using NStack;
using ReactiveUI;
using Terminal.Gui;

using Liviano.CLI.Gui.Interfaces;
using Liviano.CLI.Gui.ViewModels;
using Liviano.Models;

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

            this
                .WhenAnyValue(view => view.ViewModel.Balance)
                .BindTo(balance, balance => balance.Text);

            this
                .WhenAnyValue(view => view.ViewModel.Txs)
                .Select(txs => TxsToUString(txs))
                .Subscribe(serializedTxs => SetTransactionList(serializedTxs));

            SetTransactionList(TxsToUString(ViewModel.Txs));
        }

        void SetTransactionList(List<ustring> serializedTxs)
        {
            transactions.SetSource(serializedTxs);
        }

        List<ustring> TxsToUString(List<Tx> txs)
        {
            List<ustring> res = new() { };

            foreach (var tx in txs)
            {
                var direction = tx.IsReceive ? "Received " : "Sent    ";
                var amount = tx.IsReceive ? tx.AmountReceived.ToString() : $"-{tx.AmountSent}";
                var preposition = tx.IsReceive ? "at:  " : "from:";

                string address = string.Empty;
                if (tx.IsReceive)
                    address = tx.ScriptPubKey.GetDestinationAddress(ViewModel.Account.Network).ToString();
                else
                    address = tx.SentScriptPubKey.GetDestinationAddress(ViewModel.Account.Network).ToString();

                var localTime = tx.CreatedAt.Value.ToString("g");

                res.Add(
                    ustring.Make($"{direction} {amount} BTC {preposition} {address} {localTime}")
                );
            }

            return res;
        }

        void SetGui()
        {
            transactions = new()
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
                Width = Dim.Percent(90f),
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
