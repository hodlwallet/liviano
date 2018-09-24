using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Liviano
{
    /// <sumary>
    /// A wallet
    /// </sumary>
    public class HdAccount
    {
        /// <sumary>
        /// Init
        /// </sumary>
        public HdAccount()
        {
            this.ReceiveAddresses = new List<HdAddress>();
            this.ChangeAddresses = new List<HdAddress>();
        }

        public ICollection<HdAddress> ReceiveAddresses { get; set; }
        public ICollection<HdAddress> ChangeAddresses { get; set; }

        /// <summary>
        /// The name of this wallet.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }
}
