//
// Bip84Account.cs
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
using System.Collections.Generic;

using NBitcoin;

namespace Liviano.Accounts
{
    public class Bip84Account : Bip32Account
    {
        public override string AccountType => "bip84";
        public override string HdPathFormat => "m/84'/{0}'/{1}'";

        List<ScriptPubKeyType> scriptPubKeyTypes = new() { ScriptPubKeyType.Segwit };
        public override List<ScriptPubKeyType> ScriptPubKeyTypes
        {
            get => scriptPubKeyTypes;
            set => scriptPubKeyTypes = value;
        }

        public Bip84Account(Network network, int index = 0) : base(network, index) { }
    }
}
