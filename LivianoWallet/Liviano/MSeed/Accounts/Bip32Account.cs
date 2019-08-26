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

using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Liviano.MSeed.Accounts
{
    public abstract class Bip32Account : HdAccount
    {
        public override string AccountType => "bip32";

        // Because of hodl wallet 1.0
        public abstract string HdPathFormat { get; }

        // Our default is bech32
        const ScriptPubKeyType DEFAULT_SCRIPT_PUB_KEY_TYPE = ScriptPubKeyType.Segwit;

        ScriptPubKeyType _ScriptPubKeyType;

        [JsonProperty(PropertyName = "scriptPubKeyType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScriptPubKeyType ScriptPubKeyType
        {
            get => _ScriptPubKeyType;
            set
            {
                if (value != ScriptPubKeyType.Segwit && value != ScriptPubKeyType.Legacy && value != ScriptPubKeyType.SegwitP2SH)
                    throw new ArgumentException($"Invalid script type {value.ToString()}");

                _ScriptPubKeyType = value;
            }
        }

        public Bip32Account(int index = 0)
        {
            Id = Guid.NewGuid().ToString();
            ScriptPubKeyType = DEFAULT_SCRIPT_PUB_KEY_TYPE;

            InternalAddressesCount = 0;
            ExternalAddressesCount = 0;

            Index = index;
            HdPath = string.Format(HdPathFormat, Index);
        }

        public override BitcoinAddress GetReceiveAddress()
        {
            var pubKey = HdOperations.GeneratePublicKey(ExtendedPubKey, ExternalAddressesCount, false);
            var address = pubKey.GetAddress(ScriptPubKeyType, Network);

            ExternalAddressesCount++;

            return address;
        }

        public override BitcoinAddress[] GetReceiveAddress(int n)
        {
            var addresses = new List<BitcoinAddress>();

            for (int i = 0; i < n; i++)
            {
                var pubKey = HdOperations.GeneratePublicKey(ExtendedPubKey, ExternalAddressesCount, false);

                addresses.Add(pubKey.GetAddress(ScriptPubKeyType, Network));

                ExternalAddressesCount++;
            }

            return addresses.ToArray();
        }

        public override BitcoinAddress GetChangeAddress()
        {
            var pubKey = HdOperations.GeneratePublicKey(ExtendedPubKey, InternalAddressesCount, true);
            var address = pubKey.GetAddress(ScriptPubKeyType, Network);

            InternalAddressesCount++;

            return address;
        }

        public override BitcoinAddress[] GetChangeAddress(int n)
        {
            var addresses = new List<BitcoinAddress>();

            for (int i = 0; i < n; i++)
            {
                var pubKey = HdOperations.GeneratePublicKey(ExtendedPubKey, InternalAddressesCount, true);

                addresses.Add(pubKey.GetAddress(ScriptPubKeyType, Network));

                InternalAddressesCount++;
            }

            return addresses.ToArray();
        }
    }
}