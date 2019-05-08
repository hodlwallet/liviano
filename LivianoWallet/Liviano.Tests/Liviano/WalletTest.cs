using System;
using System.Linq;

using NBitcoin;
using Xunit;

using Liviano.Models;
using System.Collections.Generic;

namespace Liviano.Tests.Liviano
{
    public class WalletTest
    {
        [Fact]
        public void InitTest()
        {
            Wallet w = new Wallet(
                name: "1st Wallet",
                creationTime: new DateTimeOffset(DateTime.Parse("2018-10-12"))
            );

            Assert.Equal("1st Wallet", w.Name);
            Assert.Equal(Network.Main, w.Network);
            Assert.Equal(w.BlockLocator.OfType<uint256>().First(), Network.Main.GenesisHash);
            Assert.Equal(1, w.BlockLocator.Count);

            Assert.Empty(w.AccountsRoot);
            Assert.Empty(w.ChainCode);
            Assert.Empty(w.EncryptedSeed);

            Assert.True(w.IsExtPubKeyWallet);
            Assert.Equal(w.CreationTime, new DateTimeOffset(DateTime.Parse("2018-10-12")));
        }

        [Fact]
        public void CreateWalletWithMultipleAccountsOnDifferentHdPathsTest()
        {
            Network network = Network.Main;

            // Set Mnemonic and get the priv ext key
            string mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            ExtKey extKey = HdOperations.GetExtendedKey(mnemonic);
            List<AccountRoot> accountsRoot = new List<AccountRoot>
            {
                new AccountRoot(CoinType.Bitcoin, new List<HdAccount>(), "44"),
                new AccountRoot(CoinType.Bitcoin, new List<HdAccount>(), "49"),
                new AccountRoot(CoinType.Bitcoin, new List<HdAccount>(), "84")
            };

            // Creates a new wallet
            Wallet wallet = new Wallet
            {
                Name = "multiple_accounts_wallet",
                EncryptedSeed = extKey.PrivateKey.GetEncryptedBitcoinSecret("", network).ToWif(),
                ChainCode = extKey.ChainCode,
                CreationTime = DateTimeOffset.Now,
                Network = network,
                AccountsRoot = accountsRoot,
            };

            wallet.AddNewAccount(CoinType.Bitcoin, DateTimeOffset.Now, "", "44");
            wallet.AddNewAccount(CoinType.Bitcoin, DateTimeOffset.Now, "", "49");
            wallet.AddNewAccount(CoinType.Bitcoin, DateTimeOffset.Now, "", "84");

            List<HdAccount> accounts = wallet.GetAccountsByCoinType(CoinType.Bitcoin).ToList();

            for (int i = 0; i < accounts.Count; i++)
            {
                accounts[i].CreateAddresses(network, 1, false, accountsRoot[i].Purpose);
            }

            foreach (HdAccount account in accounts)
            {
                HdAddress address = account.GetFirstUnusedReceivingAddress();

                switch (account.HdPath)
                {
                    case "m/44'/0'/0'":
                    Assert.StartsWith("1", address.Address);
                    break;
                    case "m/49'/0'/0'":
                    Assert.StartsWith("3", address.Address);
                    break;
                    case "m/84'/0'/0'":
                    Assert.StartsWith("bc", address.Address);
                    break;
                    default:
                    break;
                }
            }
        }

        [Fact]
        public void IsMaximilist()
        {
            Assert.True(CoinType.Bitcoin == 0);
        }
    }
}
