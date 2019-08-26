using System;

using NBitcoin;

namespace Liviano.MSeed.Accounts
{
    public class Bip141Account : HdAccount
    {
        const ScriptType DEFAULT_SCRIPT_TYPE = ScriptType.P2WPKH;

        public override string AccountType
        {
            get => "bip141";
            set
            {
                AccountType = value;
            }
        }

        public ScriptType ScriptType {
            get => ScriptType;
            set
            {
                if (value != ScriptType.P2WPKH || value != ScriptType.P2PKH)
                    throw new ArgumentException($"Invalid script type {value.ToString()}");

                ScriptType = value;
            }
        }

        public Bip141Account()
        {
            ScriptType = DEFAULT_SCRIPT_TYPE;
        }

        public override BitcoinAddress GetReceivingAddress()
        {
            throw new NotImplementedException("Not implemented");
        }

        public override BitcoinAddress[] GetReceivingAddress(int n)
        {
            throw new NotImplementedException("Not implemented");
        }

        public override BitcoinAddress GetChangeAddress()
        {
            throw new NotImplementedException("Not implemented");
        }

        public override BitcoinAddress[] GetChangeAddress(int n)
        {
            throw new NotImplementedException("Not implemented");
        }
    }
}