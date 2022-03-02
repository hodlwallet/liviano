//
// HomeViewModel.cs
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
using System.Runtime.Serialization;

using NStack;
using ReactiveUI;

using Liviano.Interfaces;
using Liviano.Models;

namespace Liviano.CLI.Gui.ViewModels
{
    [DataContract]
    public class HomeViewModel : ReactiveObject
    {
        readonly IWallet wallet;
        public IAccount Account { get; set; }

        List<Tx> txs = new() { };
        public List<Tx> Txs
        {
            get => txs;
            set => this.RaiseAndSetIfChanged(ref txs, value);
        }

        ustring balance;
        public ustring Balance
        {
            get => balance;
            set => this.RaiseAndSetIfChanged(ref balance, value);
        }

        public HomeViewModel(IWallet wallet)
        {
            this.wallet = wallet;
            Account = wallet.CurrentAccount;

            SetBalance();
            SetTransactions();

            // Bip32 accounts only track this one to change balance
            this
                .WhenAnyValue(view => view.Account.UnspentCoins)
                .Subscribe(_ => SetBalance());

            this
                .WhenAnyValue(view => view.Account.Txs)
                .Subscribe(_ => SetTransactions());
        }

        void SetTransactions()
        {
            Txs = Account
                .Txs
                .Where(tx => tx.ScriptPubKey is not null || tx.SentScriptPubKey is not null) // FIXME there's a bug here with test mnemonic
                .OrderByDescending(tx => tx.CreatedAt).ToList();
        }

        void SetBalance()
        {
            Balance = ustring.Make($"Balance: {Account.GetBalance()} BTC");
        }
    }
}
