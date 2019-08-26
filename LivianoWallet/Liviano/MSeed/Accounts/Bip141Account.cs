using System;
using System.Collections.Generic;

using NBitcoin;

namespace Liviano.MSeed.Accounts
{
    public class Bip141Account : HdAccount
    {
        // Our default is bech32
        const ScriptPubKeyType DEFAULT_SCRIPT_PUB_KEY_TYPE = ScriptPubKeyType.Segwit;

        // This was chosen because of hodl wallet 1.0
        const string DEFAULT_HD_ROOT_PATH = "m/0'";

        public override string AccountType
        {
            get => "bip141";
            set
            {
                AccountType = value;
            }
        }

        public ScriptPubKeyType ScriptPubKeyType {
            get => ScriptPubKeyType;
            set
            {
                if (value != ScriptPubKeyType.Segwit || value != ScriptPubKeyType.Legacy)
                    throw new ArgumentException($"Invalid script type {value.ToString()}");

                ScriptPubKeyType = value;
            }
        }

        public Bip141Account()
        {
            ScriptPubKeyType = DEFAULT_SCRIPT_PUB_KEY_TYPE;

            InternalAddressesCount = 0;
            ExternalAddressesCount = 0;

            HdRootPath = DEFAULT_HD_ROOT_PATH;
        }

        public override BitcoinAddress GetReceivingAddress()
        {
            var pubKey = HdOperations.GeneratePublicKey(ExtendedPubKey, ExternalAddressesCount, false);

            return pubKey.GetAddress(ScriptPubKeyType, Network);
        }

        public override BitcoinAddress[] GetReceivingAddress(int n = GAP_LIMIT)
        {
            var addresses = new List<BitcoinAddress>();

            for (int i = 0; i < n; i++)
            {
                var pubKey = HdOperations.GeneratePublicKey(ExtendedPubKey, ExternalAddressesCount, false);

                addresses.Add(pubKey.GetAddress(ScriptPubKeyType, Network));
            }

            return addresses.ToArray();
        }

        public override BitcoinAddress GetChangeAddress()
        {
            var pubKey = HdOperations.GeneratePublicKey(ExtendedPubKey, InternalAddressesCount, true);

            return pubKey.GetAddress(ScriptPubKeyType, Network);
        }

        public override BitcoinAddress[] GetChangeAddress(int n = GAP_LIMIT)
        {
            var addresses = new List<BitcoinAddress>();

            for (int i = 0; i < n; i++)
            {
                var pubKey = HdOperations.GeneratePublicKey(ExtendedPubKey, InternalAddressesCount, true);

                addresses.Add(pubKey.GetAddress(ScriptPubKeyType, Network));
            }

            return addresses.ToArray();
        }
    }
}