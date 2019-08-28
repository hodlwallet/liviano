using Liviano.Exceptions;
using Liviano.Models;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Liviano.Tests.Liviano
{
    public class AccountRootTest
    {
        [Fact]
        public void GetFirstUnusedAccountWithoutAccountsReturnsNull()
        {
            var wallet = new Wallet();
            wallet.AccountsRoot.Add(new AccountRoot(CoinType.Bitcoin, new List<HdAccount>()));


            HdAccount result = wallet.GetFirstUnusedAccount(CoinType.Bitcoin);

            Assert.Null(result);

        }


        [Fact]
        public void GetFirstUnusedAccountReturnsAccountWithLowerIndexHavingNoAddresses()
        {

            AccountRoot accountRoot = CreateAccountRoot();

            HdAccount unused = CreateAccount("unused1");
            unused.Index = 2;
            accountRoot.Accounts.Add(unused);

            HdAccount unused2 = CreateAccount("unused2");
            unused2.Index = 1;
            accountRoot.Accounts.Add(unused2);

            HdAccount used = CreateAccount("used");
            used.ExternalAddresses.Add(CreateAddress(false));
            used.Index = 3;
            accountRoot.Accounts.Add(used);

            HdAccount used2 = CreateAccount("used2");
            used2.InternalAddresses.Add(CreateAddress(true));
            used2.Index = 4;
            accountRoot.Accounts.Add(used2);

            HdAccount result = accountRoot.GetFirstUnusedAccount();

            Assert.NotNull(result);
            Assert.Equal(1, result.Index);
            Assert.Equal("unused2", result.Name);
        }

        private HdAddress CreateAddress(bool changeAddress)
        {
            string hdPath = "1/2/3/4/5";
            if (changeAddress)
            {
                hdPath = "1/2/3/4/1";
            }
            var key = new Key();
            var address = new HdAddress
            {
                Address = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ToString(),
                HdPath = hdPath,
                ScriptPubKey = key.ScriptPubKey
            };

            return address;
        }

        private HdAccount CreateAccount(string v)
        {
            return new HdAccount
            {
                Name = v,
                HdPath = "1/2/3/4/5",
            };
        }

        private AccountRoot CreateAccountRootWithHdAccountHavingAddresses(string accountName, CoinType coinType)
        {
            return new AccountRoot(CoinType.Bitcoin,

                new List<HdAccount> {
                    new HdAccount {
                        Name = accountName,
                        InternalAddresses = new List<HdAddress>
                        {
                            CreateAddress(false),
                        },
                        ExternalAddresses = new List<HdAddress>
                        {
                            CreateAddress(false),
                        }
                    }
                });
        }

        private AccountRoot CreateAccountRoot()
        {
            return new AccountRoot(CoinType.Bitcoin, new List<HdAccount>());
        }

        [Fact]
        public void GetAccountByNameWithMatchingNameReturnsAccount()
        {
            AccountRoot accountRoot = CreateAccountRootWithHdAccountHavingAddresses("Test", CoinType.Bitcoin);

            HdAccount result = accountRoot.GetAccountByName("Test");

            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
        }

        [Fact]
        public void GetAccountByNameWithNonMatchingNameThrowsException()
        {
            AccountRoot accountRoot = CreateAccountRootWithHdAccountHavingAddresses("Test", CoinType.Bitcoin);

            Assert.Throws<WalletException>(() => { accountRoot.GetAccountByName("test"); });
        }
    }
}
