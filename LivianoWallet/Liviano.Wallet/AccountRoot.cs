using System;
using System.Collections.Generic;
using System.Linq;

using NBitcoin;
using Newtonsoft.Json;

using Liviano.Utilities;
using Liviano.Utilities.JsonConverters;

namespace Liviano
{
    public class AccountRoot
    {
        public AccountRoot()
        {
            this.Accounts = new List<HdAccount>();
        }

        /// <summary>
        /// The accounts used in the wallet.
        /// </summary>
        [JsonProperty(PropertyName = "accounts")]
        public ICollection<HdAccount> Accounts { get; set; }
    }
}
