//
// WasabiAccount.cs
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
using Liviano.Utilities.JsonConverters;
using NBitcoin;
using Newtonsoft.Json;

namespace Liviano.MSeed.Accounts
{
    public class WasabiAccount : Bip32Account
    {
        public override string AccountType => "wasabi";
        public override string HdPathFormat => "m/84'/0'/{0}'";

        public override ScriptPubKeyType ScriptPubKeyType
        {
            get => ScriptPubKeyType.Segwit;
            set
            {
                throw new ArgumentException($"Cannot be set to: {value.ToString()}, its only script pub key type is Segwit.");
            }
        }

        /// <summary>
        /// Encrypted seed usually from a mnemonic
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "encryptedSeed")]
        string EncryptedSeed { get; set; }

        /// <summary>
        /// The chain code.
        /// </summary>
        [JsonProperty(PropertyName = "chainCode", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ByteArrayConverter))]
        byte[] ChainCode { get; set; }

        public WasabiAccount() : base()
        {
        }
    }
}
