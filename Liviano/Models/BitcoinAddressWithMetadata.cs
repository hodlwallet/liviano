//
// BitcoinAddressWithMetadata.cs
//
// Author:
//       igor <igorgue@protonmail.com>
//
// Copyright (c) 2021 HODL Wallet
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
using NBitcoin;
using Newtonsoft.Json;

namespace Liviano.Models
{
    public class BitcoinAddressWithMetadata
    {
        [JsonProperty(PropertyName = "address", NullValueHandling = NullValueHandling.Ignore)]
        public BitcoinAddress Address { get; set; }

        [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
        public ScriptPubKeyType Type { get; set; }

        [JsonProperty(PropertyName = "pubKey", NullValueHandling = NullValueHandling.Ignore)]
        public string PubKey { get; set; }

        [JsonProperty(PropertyName = "hdPath", NullValueHandling = NullValueHandling.Ignore)]
        public string HdPath { get; set; }

        [JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
        public int Index { get; set; }

        // TODO Potentially having the UTXOs (an array of them) here
        // would be a good 2nd reason why to intruduce this class,
        // but for now, it would be more consistent syncing and watching.

        /// <summary>
        /// ctor
        /// <param name="address"></param>
        /// <param name="type"></param>
        /// <param name="pubKey"></param>
        /// <param name="hdPath"></param>
        /// <param name="index"></param>
        /// </summary>
        public BitcoinAddressWithMetadata(BitcoinAddress address, ScriptPubKeyType type, string pubKey, string hdPath, int index)
        {
            Address = address;
            Type = type;
            PubKey = pubKey;
            HdPath = hdPath;
            Index = index;
        }

        public override string ToString()
        {
            return Address.ToString();
        }
    }
}
