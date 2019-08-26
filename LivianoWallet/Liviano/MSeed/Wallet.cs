using System;
using System.Collections.Generic;

using NBitcoin;

using Newtonsoft.Json;

using Liviano.Utilities.JsonConverters;

using Liviano.MSeed.Interfaces;

namespace Liviano.MSeed
{
    public class Wallet : IWallet
    {
        Key _PrivateKey;

        public string[] AccountTypes => new string[] { "bip141" };

        public string Id { get; set; }

        public string Name { get; set; }

        public Network Network { get; set; }

        public string EncryptedSeed { get; set; }

        public List<string> TxIds { get; set; }

        public List<Dictionary<string, string>> AccountIds { get; set; }

        public Wallet()
        {
        }

        public void Init(Network network = null, List<string> txIds = null, List<Dictionary<string, string>> accountIds = null)
        {
            Id = Guid.NewGuid().ToString();

            Network = network ?? Network.Main;
            TxIds = txIds ?? new List<string>();
            AccountIds = accountIds ?? new List<Dictionary<string, string>>();
        }

        public Key GetPrivateKey(string password = "", bool forcePasswordVerification = false)
        {
            if (forcePasswordVerification)
            {
                _PrivateKey = HdOperations.DecryptSeed(EncryptedSeed, Network, password);

                return _PrivateKey;
            }

            if (_PrivateKey == null)
                _PrivateKey = HdOperations.DecryptSeed(EncryptedSeed, Network, password);

            return _PrivateKey;
        }
    }
}
