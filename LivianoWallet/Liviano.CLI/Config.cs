using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

namespace Liviano.CLI
{
    public class Config
    {
        /// <summary>
        /// Wallet id is the file id that will be loaded by default (the last one open as well)
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "walletId")]
        public string WalletId { get; set; }

        /// <summary>
        /// Network that the wallet is running on
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "network")]
        public string Network { get; set; }

        [JsonProperty(PropertyName = "nodesToConnect")]
        public int NodesToConnect { get; set; }

        /// <summary>
        /// Stores wallet ids to find them
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "wallets")]
        public List<string> Wallets { get; set; }

        public Config(string walletId, string network, int nodesToConnect = 4)
        {
            // Initializing with wallet id and network
            WalletId = walletId;
            Network = network;
            NodesToConnect = nodesToConnect;

            Wallets = new List<string> { walletId };
        }

        public void SaveChanges()
        {
            Save(this);
        }

        public void ReLoad()
        {
            Config loadedConfig = Load();

            WalletId = loadedConfig.WalletId;
            Network = loadedConfig.Network;
            NodesToConnect = loadedConfig.NodesToConnect;
            Wallets = loadedConfig.Wallets;
        }

        public bool HasWallet(string walletId)
        {
            return Wallets.Contains(walletId);
        }

        public void Add(string walletId)
        {
            WalletId = walletId;

            Wallets.Add(walletId);
        }

        public static void Save(Config config)
        {
            File.WriteAllText(ConfigFile(), JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public bool IsSaved()
        {
            return Exists();
        }

        public static Config Load()
        {
            var content = File.ReadAllText(ConfigFile());

            Config config = JsonConvert.DeserializeObject<Config>(content);

            return config;
        }

        public static bool Exists()
        {
            return File.Exists(ConfigFile());
        }

        public static string ConfigFile()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "liviano.json");
        }
    }
}
