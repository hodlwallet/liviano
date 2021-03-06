//
// Args.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 HODL Wallet
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
using NBitcoin;

using Liviano.Interfaces;
using Liviano.Models;

namespace Liviano.Events
{
    public class TxEventArgs : EventArgs
    {
        public Tx Tx { get; set; }
        public IAccount Account { get; set; }
        public BitcoinAddress Address { get; set; }

        public TxEventArgs(Tx tx, IAccount account, BitcoinAddress address)
        {
            Tx = tx;
            Account = account;
            Address = address;
        }
    }

    public class WatchAddressEventArgs : EventArgs
    {
        public string Notification { get; set; }
        public IAccount Account { get; set; }
        public BitcoinAddress Address { get; set; }

        public WatchAddressEventArgs(string notification, IAccount account, BitcoinAddress address)
        {
            Notification = notification;
            Account = account;
            Address = address;
        }
    }

    public class NewHeaderEventArgs : EventArgs
    {
        public IWallet Wallet { get; set; }
        public string Hex { get; set; }
        public long Height { get; set; }

        public NewHeaderEventArgs(IWallet wallet, string hex, long height)
        {
            Wallet = wallet;
            Hex = hex;
            Height = height;
        }
    }

    public class UpdatedTxConfirmationsArgs : EventArgs
    {
        public Tx Tx { get; set; }
        public long Confirmations { get; set; }

        public UpdatedTxConfirmationsArgs(Tx tx, long configurations)
        {
            Tx = tx;
            Confirmations = configurations;
        }
    }

    public class UpdatedTxCreatedAtArgs : EventArgs
    {
        public Tx Tx { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }

        public UpdatedTxCreatedAtArgs(Tx tx, DateTimeOffset? createdAt)
        {
            Tx = tx;
            CreatedAt = createdAt;
        }
    }

    public class FoundAccountEventArgs : EventArgs
    {
        public IAccount Account { get; set; }

        public FoundAccountEventArgs(IAccount account)
        {
            Account = account;
        }
    }
}
