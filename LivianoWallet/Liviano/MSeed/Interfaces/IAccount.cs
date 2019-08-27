//
// IAccount.cs
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
using System.Collections.Generic;

using Newtonsoft.Json;

using NBitcoin;

using Liviano.Utilities.JsonConverters;

namespace Liviano.MSeed.Interfaces
{
    public interface IAccount
    {
        [JsonProperty(PropertyName = "id")]
        string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }

        [JsonProperty(PropertyName = "accountType")]
        string AccountType { get; }

        [JsonProperty(PropertyName = "walletId")]
        string WalletId { get; set; }

        /// <summary>
        /// The network this wallets belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        Network Network { get; set; }

        [JsonProperty(PropertyName = "txIds")]
        List<string> TxIds { get; set; }

        /// <summary>
        /// Gets 1 receiving address
        /// </summary>
        /// <returns></returns>
        BitcoinAddress GetReceiveAddress();

        /// <summary>
        /// Gets n receiving addresses
        /// </summary>
        /// <param name="n">A <see cref="int"/> of the amount of address to generate</param>
        /// <returns></returns>
        BitcoinAddress[] GetReceiveAddress(int n);

        /// <summary>
        /// Gets 1 change address
        /// </summary>
        /// <returns></returns>
        BitcoinAddress GetChangeAddress();

        /// <summary>
        /// Gets n change addresses
        /// </summary>
        /// <param name="n">A <see cref="int"/> of the amount of address to generate</param>
        /// <returns></returns>
        BitcoinAddress[] GetChangeAddress(int n);
    }
}