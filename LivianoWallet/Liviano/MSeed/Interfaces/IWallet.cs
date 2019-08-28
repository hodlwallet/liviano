//
// IWallet.cs
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

using NBitcoin;

using Newtonsoft.Json;

using Liviano.Utilities.JsonConverters;
using System;

namespace Liviano.MSeed.Interfaces
{
    public interface IWallet
    {
        /// <summary>
        /// A list of types of accounts, e.g. "bip44", "bip141"...
        /// </summary>
        [JsonProperty(PropertyName = "accountTypes")]
        string[] AccountTypes { get; }

        /// <summary>
        /// Id of the wallet, this will be in the filesystem
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "id")]
        string Id { get; }

        /// <summary>
        /// Name to show for the wallet, probably user provided or default
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "name")]
        string Name { get; set; }

        /// <summary>
        /// The network this wallets belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "network")]
        [JsonConverter(typeof(NetworkConverter))]
        Network Network { get; set; }

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

        /// <summary>
        /// Tx ids linked to the wallet, usually this will be located also in the accounts,
        /// the wallet will find them in {walletId}/transactions
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "txIds")]
        List<string> TxIds { get; set; }

        /// <summary>
        /// Account ids of the wallet, these go under {walletId}/accounts
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "accountIds")]
        List<Dictionary<string, string>> AccountIds { get; set; }

        /// <summary>
        /// The time this wallet was created.
        /// </summary>
        [JsonProperty(PropertyName = "createdAt", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        DateTimeOffset? CreatedAt { get; set; }

        /// <summary>
        /// Init will create a new wallet initaliaing everything to their defaults,
        /// a new guid is created and the default for network is Main
        /// </summary>
        void Init(string mnemonic, string password = "", string name = null, Network network = null, DateTimeOffset? createdAt = null);

        /// <summary>
        /// Gets a private key this method also caches it on memory
        /// </summary>
        /// <param name="password">Password to decript seed to, default ""</param>
        /// <param name="forcePasswordVerification">Force the password verification, avoid cache! Default false</param>
        /// <returns></returns>
        Key GetPrivateKey(string password = "", bool forcePasswordVerification = false);

        /// <summary>
        /// Gets a extended private key this method also caches it on memory
        /// </summary>
        /// <param name="password"></param>
        /// <param name="forcePasswordVerification"></param>
        /// <returns></returns>
        ExtKey GetExtendedKey(string password = "", bool forcePasswordVerification = false);
    }
}