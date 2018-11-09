using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;

using NBitcoin;
using Serilog;

using Liviano;
using Liviano.Utilities;
using Easy.MessageHub;

namespace Liviano
{
    public class WalletManager
    {
        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        private MessageHub eventHub;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is restored.</summary>
        private const int WalletRecoveryAccountsCount = 1;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is created.</summary>
        private const int WalletCreationAccountsCount = 1;

        /// <summary>The type of coin used in this manager.</summary>
        private readonly CoinType coinType;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        ///<summary>An object capable of storing and retreiving <see cref="Wallet"/>s</summary>
        private readonly IStorageProvider storageProvider;

        /// <summary>
        /// A lock object that protects access to the <see cref="Wallet"/>.
        /// Any of the collections inside Wallet must be synchronized using this lock.
        /// </summary>
        private readonly object lockObject;

        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
        // 2. the list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
        private Dictionary<OutPoint, TransactionData> outpointLookup;
        internal Dictionary<Script, HdAddress> keysLookup;

        /// <summary>Gets the wallet.</summary>
        public Wallet Wallet { get; set; }

        /// <summary>The chain of headers.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Size of the buffer of unused addresses maintained in an account.
        /// </summary>
        private const int unusedAddressesBuffer = 20;

        /// <summary>The settings for the wallet feature.</summary>
        private readonly IScriptAddressReader scriptAddressReader;

        /// <summary>The broadcast manager.</summary>
        private readonly IBroadcastManager broadcastManager;

        public WalletManager(ILogger logger, Network network, ConcurrentChain chain, IAsyncLoopFactory asyncLoopFactory, IDateTimeProvider dateTimeProvider, IScriptAddressReader scriptAddressReader, IBroadcastManager broadcastManager, IStorageProvider storageProvider)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(scriptAddressReader, nameof(scriptAddressReader));
            Guard.NotNull(storageProvider, nameof(storageProvider));

            this.lockObject = new object();

            this.logger = logger;

            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.asyncLoopFactory = asyncLoopFactory;
            this.chain = chain;
            this.broadcastManager = broadcastManager;
            this.scriptAddressReader = scriptAddressReader;
            this.dateTimeProvider = dateTimeProvider;
            this.storageProvider = storageProvider;

            this.keysLookup = new Dictionary<Script, HdAddress>();
            this.outpointLookup = new Dictionary<OutPoint, TransactionData>();

            eventHub = MessageHub.Instance;
            eventHub.Subscribe<TransactionBroadcastEntry>(HandleBroadcastedTransaction);
        }

        /// <summary>
        /// Mehtod to handle <see cref="TransactionBroadcastEntry"/>s as they are published onto the eventhub
        /// </summary>
        /// <param name="transactionEntry"></param>
        private void HandleBroadcastedTransaction(TransactionBroadcastEntry transactionEntry)
        {
            if (string.IsNullOrEmpty(transactionEntry.ErrorMessage))
            {
                this.ProcessTransaction(transactionEntry.Transaction, null, null, transactionEntry.State == TransactionState.Propagated);
            }
            else
            {
                //this.logger.LogTrace("Exception occurred: {0}", transactionEntry.ErrorMessage);
                //this.logger.LogTrace("(-)[EXCEPTION]");
            }
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string password, string name, string passphrase, Mnemonic mnemonic = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(passphrase, nameof(passphrase));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(Wordlist.English, WordCount.Twelve);

            ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic, passphrase);

            // Create a wallet file.
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            Wallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode);

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < WalletCreationAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this.network, unusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this.network, unusedAddressesBuffer, true);
                this.UpdateKeysLookupLocked(newReceivingAddresses.Concat(newChangeAddresses));
            }

            // If the chain is downloaded, we set the height of the newly created wallet to it.
            // However, if the chain is still downloading when the user creates a wallet,
            // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet will be the height of the chain at that moment.
            if (this.chain.IsDownloaded())
            {
                this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
            }
            else
            {
                this.UpdateWhenChainDownloaded(new[] { wallet }, this.dateTimeProvider.GetUtcNow());
            }

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(wallet);
            this.Load(wallet);

            return mnemonic;
        }

        /// <inheritdoc />
        public void SaveWallet(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            lock (this.lockObject)
            {
                //Save the wallet
                this.storageProvider.SaveWallet(wallet);
            }
        }


        public bool LoadWallet(string password, out Wallet wallet)
        {
            Guard.NotEmpty(password, nameof(password));

            if (!storageProvider.WalletExists())
            {
                wallet = null;
                return false;
            }

            // Load the the wallet.
             wallet = this.storageProvider.LoadWallet();
            // Check the password.
            try
            {
                if (!wallet.IsExtPubKeyWallet)
                    Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
            }
            catch (Exception ex)
            {
                //TODO: ADD ILOGGER
                //this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                //this.logger.LogTrace("(-)[EXCEPTION]");
                throw new SecurityException(ex.Message);
            }

            this.Load(wallet);
            return true;
        }

        /// <summary>
        /// Loads the wallet to be used by the manager.
        /// </summary>
        /// <param name="wallet">The wallet to load.</param>
        private void Load(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            this.Wallet = wallet;
        }

        public static Mnemonic NewMnemonic(string wordlist = "English", int wordCount = 12)
        {
            Wordlist bitcoinWordlist;
            WordCount bitcoinWordCount;

            switch (wordlist.ToLower())
            {
                case "english":
                bitcoinWordlist = Wordlist.English;
                break;
                case "spanish":
                bitcoinWordlist = Wordlist.Spanish;
                break;
                case "chinese_simplified":
                bitcoinWordlist = Wordlist.ChineseSimplified;
                break;
                case "chinese_traditional":
                bitcoinWordlist = Wordlist.ChineseTraditional;
                break;
                case "french":
                bitcoinWordlist = Wordlist.French;
                break;
                case "japanese":
                bitcoinWordlist = Wordlist.Japanese;
                break;
                case "portuguese_brazil":
                bitcoinWordlist = Wordlist.PortugueseBrazil;
                break;
                default:
                bitcoinWordlist = Wordlist.English;
                break;
            }

            switch (wordCount)
            {
                case 12:
                bitcoinWordCount = WordCount.Twelve;
                break;
                case 15:
                bitcoinWordCount = WordCount.Fifteen;
                break;
                case 18:
                bitcoinWordCount = WordCount.Eighteen;
                break;
                case 21:
                bitcoinWordCount = WordCount.TwentyOne;
                break;
                case 24:
                bitcoinWordCount = WordCount.TwentyFour;
                break;
                default:
                bitcoinWordCount = WordCount.TwentyFour;
                break;
            }

            return NewMnemonic(bitcoinWordlist, bitcoinWordCount);
        }

        public static Mnemonic NewMnemonic(Wordlist wordlist, WordCount wordCount)
        {
            return new Mnemonic(wordlist, wordCount);
        }

        public static Mnemonic MnemonicFromString(string mnemonic)
        {
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            return new Mnemonic(mnemonic, Wordlist.AutoDetect(mnemonic));
        }


        /// <summary>
        /// Generates the wallet file.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="encryptedSeed">The seed for this wallet, password encrypted.</param>
        /// <param name="chainCode">The chain code.</param>
        /// <param name="creationTime">The time this wallet was created.</param>
        /// <returns>The wallet object that was saved into the file system.</returns>
        /// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
        private Wallet GenerateWalletFile(string name, string encryptedSeed, byte[] chainCode, DateTimeOffset? creationTime = null)
        {
            // NOTE: @igorgue: Is this needed?
            Guard.NotEmpty(name, nameof(name));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Check if any wallet file already exists, with case insensitive comparison.
            if (string.Equals(this.Wallet.Name, name, StringComparison.OrdinalIgnoreCase))
                throw new WalletException($"Wallet with name '{name}' already exists.");
        
            if (this.Wallet.EncryptedSeed != encryptedSeed)
                throw new WalletException("Cannot create this wallet as a wallet with the same private key already exists. If you want to restore your wallet make sure you have your mnemonic and your password handy!");
        
            var walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            };

            // Create a folder if none exists and persist the file.
            this.SaveWallet(walletFile);

            return walletFile;
        }

        /// <summary>
        /// Update the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void UpdateKeysLookupLocked(IEnumerable<HdAddress> addresses)
        {
            if (addresses == null || !addresses.Any())
            {
                return;
            }

            lock (this.lockObject)
            {
                foreach (HdAddress address in addresses)
                {
                    this.keysLookup[address.ScriptPubKey] = address;
                    if (address.Pubkey != null)
                        this.keysLookup[address.Pubkey] = address;
                }
            }
        }


        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            throw new NotImplementedException();
        }

            /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedBlock chainedBlock)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            // The block locator will help when the wallet
            // needs to rewind this will be used to find the fork.
            wallet.BlockLocator = chainedBlock.GetLocator().Blocks;

            lock (this.lockObject)
            {
                wallet.SetLastBlockDetailsByCoinType(this.coinType, chainedBlock);
            }
        }


        /// <summary>
        /// Updates details of the last block synced in a wallet when the chain of headers finishes downloading.
        /// </summary>
        /// <param name="wallets">The wallets to update when the chain has downloaded.</param>
        /// <param name="date">The creation date of the block with which to update the wallet.</param>
        private void UpdateWhenChainDownloaded(IEnumerable<Wallet> wallets, DateTime date)
        {
            this.asyncLoopFactory.RunUntil("WalletManager.DownloadChain", new System.Threading.CancellationToken(),
                () => this.chain.IsDownloaded(),
                () =>
                {
                    int heightAtDate = this.chain.GetHeightAtTime(date);

                    foreach (Wallet wallet in wallets)
                    {
                        this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(heightAtDate));
                        this.SaveWallet(wallet);
                    }
                },
                (ex) =>
                {
                    // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                    // sync from the current height.
                    foreach (Wallet wallet in wallets)
                    {
                        this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
                    }
                },
                TimeSpans.FiveSeconds);
        }

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string accountName = null)
        {
            var accountsHistory = new List<AccountHistory>();

            lock (this.lockObject)
            {
                var accounts = new List<HdAccount>();
                if (!string.IsNullOrEmpty(accountName))
                {
                    accounts.Add(Wallet.GetAccountByCoinType(accountName, this.coinType));
                }
                else
                {
                    accounts.AddRange(Wallet.GetAccountsByCoinType(this.coinType));
                }

                foreach (HdAccount account in accounts)
                {
                    accountsHistory.Add(this.GetHistory(account));
                }
            }

            return accountsHistory;
        }

        /// <inheritdoc />
        public AccountHistory GetHistory(HdAccount account)
        {
            Guard.NotNull(account, nameof(account));
            FlatHistory[] items;
            lock (this.lockObject)
            {
                // Get transactions contained in the account.
                items = account.GetCombinedAddresses()
                    .Where(a => a.Transactions.Any())
                    .SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToArray();
            }

            return new AccountHistory { Account = account, History = items };
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions()
        {
            var removedTransactions = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = Wallet.GetAccountsByCoinType(this.coinType);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        removedTransactions.UnionWith(address.Transactions.Select(t => (t.Id, t.CreationTime)));
                        address.Transactions.Clear();
                    }
                }
            }

            if (removedTransactions.Any())
            {
                this.SaveWallet(Wallet);
            }

            return removedTransactions;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIdsLocked(IEnumerable<uint256> transactionsIds)
        {
            Guard.NotNull(transactionsIds, nameof(transactionsIds));

            List<uint256> idsToRemove = transactionsIds.ToList();

            var result = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {
                IEnumerable<HdAccount> accounts = Wallet.GetAccountsByCoinType(this.coinType);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        for (int i = 0; i < address.Transactions.Count; i++)
                        {
                            TransactionData transaction = address.Transactions.ElementAt(i);

                            // Remove the transaction from the list of transactions affecting an address.
                            // Only transactions that haven't been confirmed in a block can be removed.
                            if (!transaction.IsConfirmed() && idsToRemove.Contains(transaction.Id))
                            {
                                result.Add((transaction.Id, transaction.CreationTime));
                                address.Transactions = address.Transactions.Except(new[] { transaction }).ToList();
                                i--;
                            }

                            // Remove the spending transaction object containing this transaction id.
                            if (transaction.SpendingDetails != null && !transaction.SpendingDetails.IsSpentConfirmed() && idsToRemove.Contains(transaction.SpendingDetails.TransactionId))
                            {
                                result.Add((transaction.SpendingDetails.TransactionId, transaction.SpendingDetails.CreationTime));
                                address.Transactions.ElementAt(i).SpendingDetails = null;
                            }
                        }
                    }
                }
            }

            if (result.Any())
            {
                this.SaveWallet(Wallet);
            }

            return result;
        }

        /// <inheritdoc />
        public DateTimeOffset GetOldestWalletCreationTime()
        {
            // NOTE: @igorgue for now we gonna keep this, even though it's not gonna be used
            return Wallet.CreationTime;
        }

        /// <inheritdoc />
        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            return Wallet.BlockLocator;
        }

        /// <inheritdoc />
        public int? GetEarliestWalletHeight()
        {
            return Wallet.AccountsRoot.Min().LastBlockSyncedHeight;
        }

        /// <inheritdoc />
        public Wallet GetWalletByName()
        {
            return Wallet;
        }
    }
}
