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
using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

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

        readonly List<View> controls = new() { };
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
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(balance, balance => balance.Text);

            this
                .WhenAnyValue(view => view.ViewModel.Txs)
                .Select(txs => TxsToUString(txs))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(serializedTxs => SetTransactionList(serializedTxs));

            this
                .WhenAnyValue(view => view.ViewModel.Status)
                .ObserveOn(RxApp.MainThreadScheduler)
                .BindTo(status, status => status.Text);

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

                string address;
                if (tx.IsReceive)
                    address = tx.ScriptPubKey.GetDestinationAddress(ViewModel.Wallet.Network).ToString();
                else
                    address = tx.SentScriptPubKey.GetDestinationAddress(ViewModel.Wallet.Network).ToString();

                address = address.PadRight(62);

                var localTime = tx.CreatedAt.Value.ToString("g");

                res.Add(
                    ustring.Make($"{direction} {amount} BTC {preposition} {address} {localTime}")
                );
            }

            return res;
        }

        void SetGui()
        {
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

            transactionsTitle = new("Transactions:")
            {
                X = 0,
                Y = 2,
                Width = Dim.Fill(0),
            };

            controls.Add(transactionsTitle);

            transactions = new()
            {
                X = 0,
                Y = 4,
                Width = Dim.Fill(0),
                Height = Dim.Fill(0),
                CanFocus = true,
            };

            transactions.KeyUp += Transactions_KeyUp;

            controls.Add(transactions);
        }

        void Transactions_KeyUp(View.KeyEventEventArgs args)
        {
            if (transactions.Source.Count == 0) return;

            var key = args.KeyEvent.Key;
            if (key == Key.Enter || key == Key.Space) OpenTransactionDetails();
        }

        void OpenTransactionDetails()
        {
            Debug.WriteLine("[OpenTransactionDetails] Opening transaction details");

            var tx = ViewModel.Txs[transactions.SelectedItem];
            var baseUrl = $"https://blockstream.info/{tx.Network.ToString().ToLower()}";

            var closeButton = new Button("Close");
            var openButton = new Button("Open In Browser");
            var dialog = new Dialog("Transaction Detail", 85, 11, closeButton, openButton);

            closeButton.Clicked += () => Application.RequestStop();
            openButton.Clicked += () => OpenUrl($"{baseUrl}/tx/{tx.Id}");

            var idLabel = new Label("Id:           ") { X = 0, Y = 1 };
            var txId = new Label(tx.Id.ToString()) { X = Pos.Right(idLabel), Y = 1, CanFocus = true };
            txId.KeyUp += (args) =>
            {
                if (args.KeyEvent.Key == Key.Space || args.KeyEvent.Key == Key.Enter)
                    OpenUrl($"{baseUrl}/tx/{tx.Id}");
            };

            dialog.Add(idLabel);
            dialog.Add(txId);

            dialog.Add(new Label($"Created At:   {tx.CreatedAt.Value}") { X = 0, Y = 2 });

            var blockHeightLabel = new Label("Block Height: ") { X = 0, Y = 3 };
            var blockHeight = new Label(tx.BlockHeight.ToString()) { X = Pos.Right(blockHeightLabel), Y = 3, CanFocus = true };
            blockHeight.KeyUp += (args) =>
            {
                if (args.KeyEvent.Key == Key.Space || args.KeyEvent.Key == Key.Enter)
                    OpenUrl($"{baseUrl}/block/{tx.Blockhash}");
            };

            dialog.Add(blockHeightLabel);
            dialog.Add(blockHeight);

            var address = string.Empty;
            var addressPreposition = string.Empty;
            var amount = string.Empty;

            if (tx.IsReceive)
            {
                address = tx.ScriptPubKey.GetDestinationAddress(tx.Network).ToString();
                addressPreposition = "At:           ";
                amount = $"Amount:       {tx.AmountReceived}";
            }
            else
            {
                address = tx.SentScriptPubKey.GetDestinationAddress(tx.Network).ToString();
                addressPreposition = $"From:         ";
                amount = $"Amount:       {tx.AmountSent}";
            }

            var addressPrepositionLabel = new Label(addressPreposition) { X = 0, Y = 4 };
            var addressLabel = new Label(address) { X = Pos.Right(addressPrepositionLabel), Y = 4, CanFocus = true };
            addressLabel.KeyUp += (args) =>
            {
                if (args.KeyEvent.Key == Key.Space || args.KeyEvent.Key == Key.Enter)
                    OpenUrl($"{baseUrl}/address/{address}");
            };

            dialog.Add(addressPrepositionLabel);
            dialog.Add(addressLabel);

            dialog.Add(new Label(amount) { X =0, Y = 5 });

            dialog.Add(new Label($"Fees:         {tx.TotalFees}") { X = 0, Y = 6 });

            Application.Run(dialog);
        }

        static void OpenUrl(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "xdg-open",
                            Arguments = url,
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        }
                    };
                    process.Start();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
            }
            catch
            {
                throw;
            }
        }
    }
}
