using Liviano.Exceptions;
using Liviano.Models;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Liviano.Tests.Liviano
{
    public class HdAccountTest
    {
        [Fact]
        public void GetCoinTypeHavingHdPathReturnsCointType()
        {
            var account = new HdAccount();
            account.HdPath = "m/84'/0'";

            CoinType result = account.GetCoinType();

            Assert.Equal(CoinType.Bitcoin, result);
        }

        [Fact]
        public void GetCoinTypeWithInvalidHdPathThrowsFormatException()
        {
            Assert.Throws<WalletException>(() =>
            {
                var account = new HdAccount();
                account.HdPath = "1/";

                account.GetCoinType();
            });
        }

        [Fact]
        public void GetCoinTypeWithoutHdPathThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var account = new HdAccount();
                account.HdPath = null;

                account.GetCoinType();
            });
        }

        [Fact]
        public void GetCoinTypeWithEmptyHdPathThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var account = new HdAccount();
                account.HdPath = string.Empty;

                account.GetCoinType();
            });
        }

        [Fact]
        public void GetFirstUnusedReceivingAddressWithExistingUnusedReceivingAddressReturnsAddressWithLowestIndex()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 3 });
            account.ExternalAddresses.Add(new HdAddress { Index = 2 });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetFirstUnusedReceivingAddress();

            Assert.Equal(account.ExternalAddresses.ElementAt(1), result);
        }

        [Fact]
        public void GetFirstUnusedReceivingAddressWithoutExistingUnusedReceivingAddressReturnsNull()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData() } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetFirstUnusedReceivingAddress();

            Assert.Null(result);
        }

        [Fact]
        public void GetFirstUnusedReceivingAddressWithoutReceivingAddressReturnsNull()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Clear();

            HdAddress result = account.GetFirstUnusedReceivingAddress();

            Assert.Null(result);
        }

        [Fact]
        public void GetFirstUnusedChangeAddressWithExistingUnusedChangeAddressReturnsAddressWithLowestIndex()
        {
            var account = new HdAccount();
            account.InternalAddresses.Add(new HdAddress { Index = 3 });
            account.InternalAddresses.Add(new HdAddress { Index = 2 });
            account.InternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetFirstUnusedChangeAddress();

            Assert.Equal(account.InternalAddresses.ElementAt(1), result);
        }

        [Fact]
        public void GetFirstUnusedChangeAddressWithoutExistingUnusedChangeAddressReturnsNull()
        {
            var account = new HdAccount();
            account.InternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData() } });
            account.InternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetFirstUnusedChangeAddress();

            Assert.Null(result);
        }

        [Fact]
        public void GetFirstUnusedChangeAddressWithoutChangeAddressReturnsNull()
        {
            var account = new HdAccount();
            account.InternalAddresses.Clear();

            HdAddress result = account.GetFirstUnusedChangeAddress();

            Assert.Null(result);
        }

        [Fact]
        public void GetLastUsedAddressWithChangeAddressesHavingTransactionsReturnsHighestIndex()
        {
            var account = new HdAccount();
            account.InternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData() } });
            account.InternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData() } });
            account.InternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetLastUsedAddress(isChange: true);

            Assert.Equal(account.InternalAddresses.ElementAt(1), result);
        }

        [Fact]
        public void GetLastUsedAddressLookingForChangeAddressWithoutChangeAddressesHavingTransactionsReturnsNull()
        {
            var account = new HdAccount();
            account.InternalAddresses.Add(new HdAddress { Index = 2 });
            account.InternalAddresses.Add(new HdAddress { Index = 3 });
            account.InternalAddresses.Add(new HdAddress { Index = 1 });

            HdAddress result = account.GetLastUsedAddress(isChange: true);

            Assert.Null(result);
        }

        [Fact]
        public void GetLastUsedAddressLookingForChangeAddressWithoutChangeAddressesReturnsNull()
        {
            var account = new HdAccount();
            account.InternalAddresses.Clear();

            HdAddress result = account.GetLastUsedAddress(isChange: true);

            Assert.Null(result);
        }

        [Fact]
        public void GetLastUsedAddressWithReceivingAddressesHavingTransactionsReturnsHighestIndex()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData() } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData() } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData() } });

            HdAddress result = account.GetLastUsedAddress(isChange: false);

            Assert.Equal(account.ExternalAddresses.ElementAt(1), result);
        }

        [Fact]
        public void GetLastUsedAddressLookingForReceivingAddressWithoutReceivingAddressesHavingTransactionsReturnsNull()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2 });
            account.ExternalAddresses.Add(new HdAddress { Index = 3 });
            account.ExternalAddresses.Add(new HdAddress { Index = 1 });

            HdAddress result = account.GetLastUsedAddress(isChange: false);

            Assert.Null(result);
        }

        [Fact]
        public void GetLastUsedAddressLookingForReceivingAddressWithoutReceivingAddressesReturnsNull()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Clear();

            HdAddress result = account.GetLastUsedAddress(isChange: false);

            Assert.Null(result);
        }

        [Fact]
        public void GetTransactionsByIdHavingTransactionsWithIdReturnsTransactions()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 8 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 9 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });

            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 5, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 11 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 12 } } });

            IEnumerable<TransactionData> result = account.GetTransactionsById(new uint256(18));

            Assert.Equal(2, result.Count());
            Assert.Equal(8, result.ElementAt(0).Index);
            Assert.Equal(new uint256(18), result.ElementAt(0).Id);
            Assert.Equal(11, result.ElementAt(1).Index);
            Assert.Equal(new uint256(18), result.ElementAt(1).Id);
        }

        [Fact]
        public void GetTransactionsByIdHavingNoMatchingTransactionsReturnsEmptyList()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10 } } });

            IEnumerable<TransactionData> result = account.GetTransactionsById(new uint256(20));

            Assert.Empty(result);
        }

        [Fact]
        public void GetSpendableTransactionsWithSpendableTransactionsReturnsSpendableTransactions()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7, SpendingDetails = new SpendingDetails() } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 8 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 9, SpendingDetails = new SpendingDetails() } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });

            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10, SpendingDetails = new SpendingDetails() } } });
            account.InternalAddresses.Add(new HdAddress { Index = 5, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 11 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 12, SpendingDetails = new SpendingDetails() } } });

            IEnumerable<TransactionData> result = account.GetSpendableTransactions();

            Assert.Equal(2, result.Count());
            Assert.Equal(8, result.ElementAt(0).Index);
            Assert.Equal(new uint256(18), result.ElementAt(0).Id);
            Assert.Equal(11, result.ElementAt(1).Index);
            Assert.Equal(new uint256(18), result.ElementAt(1).Id);
        }

        [Fact]
        public void GetSpendableTransactionsWithoutSpendableTransactionsReturnsEmptyList()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7, SpendingDetails = new SpendingDetails() } } });
            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10, SpendingDetails = new SpendingDetails() } } });

            IEnumerable<TransactionData> result = account.GetSpendableTransactions();

            Assert.Empty(result);
        }

        [Fact]
        public void FindAddressesForTransactionWithMatchingTransactionsReturnsTransactions()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 8 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 9 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });

            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 5, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 11 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 12 } } });

            IEnumerable<HdAddress> result = account.FindAddressesForTransaction(t => t.Id == 18);

            Assert.Equal(2, result.Count());
            Assert.Equal(3, result.ElementAt(0).Index);
            Assert.Equal(5, result.ElementAt(1).Index);
        }

        [Fact]
        public void FindAddressesForTransactionWithoutMatchingTransactionsReturnsEmptyList()
        {
            var account = new HdAccount();
            account.ExternalAddresses.Add(new HdAddress { Index = 2, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 7 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 3, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 8 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 1, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 9 } } });
            account.ExternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });

            account.InternalAddresses.Add(new HdAddress { Index = 4, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(15), Index = 10 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 5, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(18), Index = 11 } } });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = null });
            account.InternalAddresses.Add(new HdAddress { Index = 6, Transactions = new List<TransactionData> { new TransactionData { Id = new uint256(19), Index = 12 } } });

            IEnumerable<HdAddress> result = account.FindAddressesForTransaction(t => t.Id == 25);

            Assert.Empty(result);
        }

        [Fact]
        public void ImportHodlWallet1Seed()
        {
            Network network = Network.Main;
            string mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            string hdPath = "m/0'/0/0";
            string firstAddress = "bc1qgv52mt89gpev6p56huggl970sppqkgftxakv7f";

            ExtKey extKey = HdOperations.GetExtendedKey(mnemonic);

            // First lets check with normal pub key derivation
            PubKey pubKey = extKey.Derive(new KeyPath(hdPath)).Neuter().PubKey;
            Assert.Equal(firstAddress, pubKey.WitHash.GetAddress(network).ToString());

            List<AccountRoot> accountsRoot = new List<AccountRoot>
            {
                new AccountRoot(CoinType.Bitcoin, new List<HdAccount>(), "141")
            };

            // Creates a new wallet
            Wallet wallet = new Wallet
            {
                Name = "hodl_wallet_old_wallet",
                EncryptedSeed = extKey.PrivateKey.GetEncryptedBitcoinSecret("", network).ToWif(),
                ChainCode = extKey.ChainCode,
                CreationTime = DateTimeOffset.Now,
                Network = network,
                AccountsRoot = accountsRoot
            };

            wallet.AddNewAccount(CoinType.Bitcoin, DateTimeOffset.Now, "", "141");

            HdAccount account = wallet.GetAccountByCoinType("Account 0", CoinType.Bitcoin);
            account.CreateAddresses(network, 1, false, "141");

            HdAddress address = account.GetFirstUnusedReceivingAddress();

            Console.WriteLine($"Address' hd path: {address.HdPath}");

            Assert.Equal(hdPath, address.HdPath);
            Assert.Equal(firstAddress, address.Address);
        }
    }
}
