//
// WasabiAccount.cs
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
using NBitcoin;
using Newtonsoft.Json;

using Liviano.Bips;
using Liviano.Extensions;
using Liviano.Utilities;

namespace Liviano.Accounts
{
    public class WasabiAccount : Bip84Account
    {
        public override string AccountType => "wasabi";

        Key _PrivateKey;

        ExtKey _ExtKey;

        ExtKey _AccountExtKey;

        /// <summary>
        /// Encrypted seed usually from a mnemonic
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "encryptedSeed")]
        public string EncryptedSeed { get; set; }

        /// <summary>
        /// The chain code.
        /// </summary>
        [JsonProperty(PropertyName = "chainCode", NullValueHandling = NullValueHandling.Ignore)]
        public byte[] ChainCode { get; set; }

        public WasabiAccount(int index = 0) : base(index)
        {
            Index = index;
        }

        public WasabiAccount(string mnemonic, string password = "", Network network = null, int index = 0) : base(index)
        {
            var mnemonicObj = Hd.MnemonicFromString(mnemonic);
            var extKey = Hd.GetExtendedKey(mnemonicObj, password);

            Network = network;
            EncryptedSeed = extKey.PrivateKey.GetEncryptedBitcoinSecret(password, network).ToWif();
            ChainCode = extKey.ChainCode;

            _ = GetPrivateKey(password);
            _ = GetExtendedKey(password);

            _AccountExtKey = _ExtKey.Derive(
               new KeyPath(HdPath)
            );

            ExtendedPrivKey = _AccountExtKey.ToString(Network);
            ExtendedPubKey = _AccountExtKey.Neuter().ToString(Network);
        }

        public Key GetPrivateKey(string password = "", bool forcePasswordVerification = false)
        {
            if (_PrivateKey == null || forcePasswordVerification)
                _PrivateKey = Hd.DecryptSeed(EncryptedSeed, Network, password);

            return _PrivateKey;
        }

        public ExtKey GetExtendedKey(string password = "", bool forcePasswordVerification = false)
        {
            Guard.NotNull(_PrivateKey, nameof(_PrivateKey));
            Guard.NotNull(ChainCode, nameof(ChainCode));

            if (forcePasswordVerification)
                _PrivateKey = GetPrivateKey(password, forcePasswordVerification);

            if (_ExtKey is null || forcePasswordVerification)
                _ExtKey = new ExtKey(_PrivateKey, ChainCode);

            return _ExtKey;
        }

        public new static WasabiAccount Create(string name, object options)
        {
            var kwargs = options.ToDict();

            var mnemonic = (string)kwargs.TryGet("Mnemonic");
            var password = (string)kwargs.TryGet("Password");
            var network = (Network)kwargs.TryGet("Network");

            var account = new WasabiAccount(mnemonic, password, network)
            {
                Name = name
            };

            return account;
        }
    }
}
