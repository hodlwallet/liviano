//
// Config.cs
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

        /// <summary>
        /// Stores wallet ids to find them
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "wallets")]
        public List<string> Wallets { get; set; }

        /// <summary>
        /// Initializer
        /// </summary>
        /// <param name="walletId">Wallet id as a <see cref="string"/></param>
        /// <param name="network">Network as a <see cref="string"/></param>
        public Config(string walletId, string network)
        {
            // Initializing with wallet id and network
            Network = network;

            if (string.IsNullOrWhiteSpace(walletId))
            {
                WalletId = "";
                Wallets = new List<string> { };
            }
            else
            {
                WalletId = walletId;
                Wallets = new List<string> { walletId };
            }
        }

        /// <summary>
        /// Saves changes on the config
        /// </summary>
        public void SaveChanges()
        {
            Save(this);
        }

        /// <summary>
        /// Reloads the config
        /// </summary>
        public void ReLoad()
        {
            Config loadedConfig = Load();

            WalletId = loadedConfig.WalletId;
            Network = loadedConfig.Network;
            Wallets = loadedConfig.Wallets;
        }

        /// <summary>
        /// Check if the config has a walletId
        /// </summary>
        /// <param name="walletId">Walelt id as a <see cref="string"/></param>
        /// <returns>True if exists</returns>
        public bool HasWallet(string walletId)
        {
            return Wallets.Contains(walletId);
        }

        /// <summary>
        /// Add wallet to the config
        /// </summary>
        /// <param name="walletId">A <see cref="string"/> the walletId</param>
        public void AddWallet(string walletId)
        {
            if (string.IsNullOrWhiteSpace(walletId)) return;

            WalletId = walletId;

            if (!HasWallet(walletId)) Wallets.Add(walletId);
        }

        /// <summary>
        /// Saves a config into the filesystem
        /// </summary>
        /// <param name="config">A <see cref="Config"/> instance to save</param>
        public static void Save(Config config)
        {
            File.WriteAllText(ConfigFile(), JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        /// <summary>
        /// Check if the config exist in the filesystem
        /// </summary>
        public bool IsSaved()
        {
            return Exists();
        }

        /// <summary>
        /// Loads a config
        /// </summary>
        public static Config Load()
        {
            var content = File.ReadAllText(ConfigFile());

            Config config = JsonConvert.DeserializeObject<Config>(content);

            return config;
        }

        /// <summary>
        /// Checks if the config file exists
        /// </summary>
        /// <returns>True if it exists</returns>
        public static bool Exists()
        {
            return File.Exists(ConfigFile());
        }

        /// <summary>
        /// Get where the config file is
        /// </summary>
        /// <returns>A <see cref="string"/> the config file location</returns>
        public static string ConfigFile()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "liviano.json");
        }
    }
}
