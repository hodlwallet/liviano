//
// PaperAccount.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2019 
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

using Newtonsoft.Json;

using NBitcoin;

using Liviano.MSeed.Interfaces;
using Liviano.Utilities;

namespace Liviano.MSeed.Accounts
{
    public class PaperAccount : IAccount
    {
        public string AccountType => "paper";

        public const ScriptPubKeyType DEFAULT_SCRIPT_PUB_KEY_TYPE = ScriptPubKeyType.Segwit;

        public string Id { get; set; }

        public string WalletId { get; set; }

        public Network Network { get; set; }

        public string Name { get; set; }

        public List<string> TxIds { get; set; }

        public ScriptPubKeyType ScriptPubKeyType { get; set; }

        [JsonProperty(PropertyName = "privateKey")]
        public Key PrivateKey { get; set; }

        [JsonProperty(PropertyName = "publicKey")]
        public PubKey PublicKey { get; set; }

        public PaperAccount(string name, string wif = null, Network network = null)
        {
            Guard.NotNull(name, nameof(name));

            var scriptPubKeyType = DEFAULT_SCRIPT_PUB_KEY_TYPE;

            Initialize(name, scriptPubKeyType, wif, network);
        }

        public PaperAccount(string name, ScriptPubKeyType scriptPubKeyType, string wif = null, Network network = null)
        {
            Initialize(name, scriptPubKeyType, wif, network);
        }

        public BitcoinAddress GetChangeAddress()
        {
            throw new ArgumentException($"Paper accounts cannot generate change addresses");
        }

        public BitcoinAddress[] GetChangeAddress(int n)
        {
            throw new ArgumentException($"Paper accounts cannot generate change addresses");
        }

        public BitcoinAddress GetReceiveAddress()
        {
            Guard.NotNull(PublicKey, nameof(PublicKey));

            return PublicKey.GetAddress(ScriptPubKeyType, Network);
        }

        public BitcoinAddress[] GetReceiveAddress(int n)
        {
            throw new ArgumentException($"Paper accounts cannot generate more than 1 and only 1 address");
        }

        void Initialize(string name, ScriptPubKeyType scriptPubKeyType, string wif = null, Network network = null)
        {
            Guard.NotNull(name, nameof(name));
            Guard.NotNull(scriptPubKeyType, nameof(scriptPubKeyType));

            Id = Guid.NewGuid().ToString();
            Name = name;
            ScriptPubKeyType = scriptPubKeyType;
            Network = Network ?? network ?? Network.Main;

            if (wif is null)
            {
                PrivateKey = new Key();
                PublicKey = PrivateKey.PubKey;
            }
            else
            {
                PrivateKey = Key.Parse(wif, Network);
                PublicKey = PrivateKey.PubKey;
            }
        }
    }
}
