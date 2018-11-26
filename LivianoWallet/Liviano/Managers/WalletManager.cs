using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using NBitcoin;
using Serilog;
using Liviano.Utilities;
using Easy.MessageHub;
using Liviano.Models;
using Liviano.Interfaces;
using Liviano.Enums;
using Liviano.Exceptions;

namespace Liviano.Managers
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
        private readonly double WalletSavetimeIntervalInMinutes = 1;

        public uint256 WalletTipHash { get; set; }

        private IAsyncLoop asyncLoop;

        public WalletManager(ILogger logger, Network network, ConcurrentChain chain, IAsyncLoopFactory asyncLoopFactory, IDateTimeProvider dateTimeProvider, IScriptAddressReader scriptAddressReader, IStorageProvider storageProvider)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(scriptAddressReader, nameof(scriptAddressReader));
            Guard.NotNull(storageProvider, nameof(storageProvider));

            this.lockObject = new object();

            this.logger = logger;

            this.network = network;
            this.coinType = CoinType.Bitcoin;
            this.asyncLoopFactory = asyncLoopFactory;
            this.chain = chain;
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


        public void Start()
        {
            // Find wallets and load them in memory.
            Wallet = storageProvider.LoadWallet();


                foreach (HdAccount account in Wallet.GetAccountsByCoinType(this.coinType))
                {
                    this.AddAddressesToMaintainBuffer(account, false);
                    this.AddAddressesToMaintainBuffer(account, true);
                }

            // Load data in memory for faster lookups.
            this.LoadKeysLookupLock();

            // Find the last chain block received by the wallet manager.
            this.WalletTipHash = this.LastReceivedBlockHash();

            // Save the wallets file every 5 minutes to help against crashes.
            this.asyncLoop = this.asyncLoopFactory.Run("Wallet persist job", token =>
            {
                this.SaveWallet(Wallet);
                this.logger.Information("Wallets saved to file at {0}.", this.dateTimeProvider.GetUtcNow());

                this.logger.Information("(-)[IN_ASYNC_LOOP]");
                return Task.CompletedTask;
            },
            repeatEvery: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes),
            startAfter: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes));
        }


        public uint256 LastReceivedBlockHash()
        {
            if (Wallet == null)
            {
                uint256 hash = this.chain.Tip.HashBlock;
                this.logger.Information("(-)[NO_WALLET]:'{0}'", hash);
                return hash;
            }

            uint256 lastBlockSyncedHash;
            lock (this.lockObject)
            {
                //lastBlockSyncedHash = this.Wallet
                //    .Select(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType))
                //    .Where(w => w != null)
                //    .OrderBy(o => o.LastBlockSyncedHeight)
                //    .FirstOrDefault()?.LastBlockSyncedHash;

                 lastBlockSyncedHash = Wallet.AccountsRoot.Where(x => x.CoinType == CoinType.Bitcoin).Where(w => w != null)
                    .OrderBy(o => o.LastBlockSyncedHeight)
                    .FirstOrDefault()?.LastBlockSyncedHash;

                // If details about the last block synced are not present in the wallet,
                // find out which is the oldest wallet and set the last block synced to be the one at this date.
                if (lastBlockSyncedHash == null)
                {
                    this.logger.Warning("There were no details about the last block synced in the wallets.");
                    DateTimeOffset earliestWalletDate = Wallet.CreationTime;
                    this.UpdateWhenChainDownloaded(Wallet, earliestWalletDate.DateTime);
                    lastBlockSyncedHash = this.chain.Tip.HashBlock;
                }
            }

            return lastBlockSyncedHash;
        }


        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void LoadKeysLookupLock()
        {
            lock (this.lockObject)
            {

                    IEnumerable<HdAddress> addresses = Wallet.GetAllAddressesByCoinType(CoinType.Bitcoin);
                    foreach (HdAddress address in addresses)
                    {
                        this.keysLookup[address.P2PKH_ScriptPubKey] = address;
                        this.keysLookup[address.P2WPKH_ScriptPubKey] = address;
                        this.keysLookup[address.P2SH_P2WPKH_ScriptPubKey] = address;

                        if (address.Pubkey != null)
                        this.keysLookup[address.Pubkey] = address;

                        foreach (TransactionData transaction in address.Transactions)
                        {
                            this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                        }
                    }
            }
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string password, string name, Mnemonic mnemonic = null, string wordlist = "english", int wordCount = 12)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));
           // Guard.NotNull(passphrase, nameof(passphrase));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(HdOperations.WordlistFromString(wordlist), HdOperations.WordCountFromInt(wordCount));

            ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic);

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
                this.UpdateWhenChainDownloaded(wallet, this.dateTimeProvider.GetUtcNow());
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
                this.logger.Information("Saving wallet {walletName} to storage provider", wallet.Name);
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
                this.logger.Error("Exception occurred: {0}", ex.ToString());
                this.logger.Error("(-)[EXCEPTION]");
                throw new SecurityException(ex.Message);
            }

            logger.Information("Wallet {walletName} loaded from the storage provider", wallet.Name);
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
            this.logger.Information("Wallet {walletName} has been loaded into the WalletManager", wallet.Name);
            this.Wallet = wallet;
        }

        public static Mnemonic NewMnemonic(string wordlist = "english", int wordCount = 12)
        {
            Wordlist bitcoinWordlist = HdOperations.WordlistFromString(wordlist);
            WordCount bitcoinWordCount = HdOperations.WordCountFromInt(wordCount);

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

            if (Wallet != null) //On start wallet is null
            {
                if (string.Equals(this.Wallet.Name, name, StringComparison.OrdinalIgnoreCase))
                    throw new WalletException($"Wallet with name '{name}' already exists.");

                if (this.Wallet.EncryptedSeed != encryptedSeed)
                    throw new WalletException("Cannot create this wallet as a wallet with the same private key already exists. If you want to restore your wallet make sure you have your mnemonic and your password handy!");

            }

            var walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
                Network = this.network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<HdAccount>(), CoinType = this.coinType } },
            };

            this.logger.Information("Wallet file created for wallet {walletName}", walletFile.Name);

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
                    this.keysLookup[address.P2PKH_ScriptPubKey] = address;
                    this.keysLookup[address.P2WPKH_ScriptPubKey] = address;
                    this.keysLookup[address.P2SH_P2WPKH_ScriptPubKey] = address;

                    if (address.Pubkey != null)
                        this.keysLookup[address.Pubkey] = address;
                }
            }
        }

        private void AddInputKeysLookupLocked(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));

            lock (this.lockObject)
            {
                this.outpointLookup[new OutPoint(transactionData.Id, transactionData.Index)] = transactionData;
            }
        }


        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, MerkleBlock block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();

            bool foundReceivingTrx = false, foundSendingTrx = false;

            lock (this.lockObject)
            {
                // Check the outputs.
                foreach (TxOut utxo in transaction.Outputs)
                {
                    // Check if the outputs contain one of our addresses.
                    if (this.keysLookup.TryGetValue(utxo.ScriptPubKey, out HdAddress _))
                    {
                        this.AddTransactionToWallet(transaction, utxo, blockHeight, block, isPropagated);
                        foundReceivingTrx = true;
                    }
                }

                // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
                foreach (TxIn input in transaction.Inputs)
                {
                    if (!this.outpointLookup.TryGetValue(input.PrevOut, out TransactionData tTx))
                    {
                        continue;
                    }

                    // Get the details of the outputs paid out.
                    IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
                    {
                        // If script is empty ignore it.
                        if (o.Value == 0)
                            return false;

                        // Check if the destination script is one of the wallet's.
                        bool found = this.keysLookup.TryGetValue(o.ScriptPubKey, out HdAddress addr);

                        // Include the keys not included in our wallets (external payees).
                        if (!found)
                            return true;

                        // Include the keys that are in the wallet but that are for receiving
                        // addresses (which would mean the user paid itself).
                        // We also exclude the keys involved in a staking transaction.
                        return !addr.IsChangeAddress();/* && !transaction.IsCoinStake;*/
                    });

                    this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                    foundSendingTrx = true;
                }
            }


            // Figure out what to do when this transaction is found to affect the wallet.
            if (foundSendingTrx || foundReceivingTrx)
            {
                // Save the wallet when the transaction was not included in a block.
                if (blockHeight == null)
                {
                    this.SaveWallet(Wallet);
                }
            }

            return foundSendingTrx || foundReceivingTrx;
        }


        /// <summary>
        /// Mark an output as spent, the credit of the output will not be used to calculate the balance.
        /// The output will remain in the wallet for history (and reorg).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="paidToOutputs">A list of payments made out</param>
        /// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
        /// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        private void AddSpendingTransactionToWallet(Transaction transaction, IEnumerable<TxOut> paidToOutputs,
            uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, MerkleBlock block = null)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(paidToOutputs, nameof(paidToOutputs));

            // Get the transaction being spent.
            TransactionData spentTransaction = this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions)
                .SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
            if (spentTransaction == null)
            {
                // Strange, why would it be null?
                this.logger.Information("(-)[TX_NULL]");
                return;
            }

            // If the details of this spending transaction are seen for the first time.
            if (spentTransaction.SpendingDetails == null)
            {
                //TODO: Event - Add event to notify new spending transcation found
                this.logger.Information("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

                var payments = new List<PaymentDetails>();
                foreach (TxOut paidToOutput in paidToOutputs)
                {
                    // Figure out how to retrieve the destination address.
                    string destinationAddress = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, paidToOutput.ScriptPubKey);
                    if (destinationAddress == string.Empty)
                        if (this.keysLookup.TryGetValue(paidToOutput.ScriptPubKey, out HdAddress destination))
                            destinationAddress = destination.Address;

                    payments.Add(new PaymentDetails
                    {
                        DestinationScriptPubKey = paidToOutput.ScriptPubKey,
                        DestinationAddress = destinationAddress,
                        Amount = paidToOutput.Value
                    });
                }

                var spendingDetails = new SpendingDetails
                {
                    TransactionId = transaction.GetHash(),
                    Payments = payments,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.BlockTime.Ticks ?? 0/*?? transaction.Time*/),
                    BlockHeight = blockHeight,
                    Hex = true ? transaction.ToHex() : null,
                    IsCoinStake = /*transaction.IsCoinStake ==*/ false /*? (bool?)null : true*/
                };

                spentTransaction.SpendingDetails = spendingDetails;
                spentTransaction.MerkleProof = block.PartialMerkleTree;
            }
            else // If this spending transaction is being confirmed in a block.
            {
                //TODO: Event - Add event to notify of spentTransaction being updated
                this.logger.Information("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

                // Update the block height.
                if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
                {
                    spentTransaction.SpendingDetails.BlockHeight = blockHeight;
                }

                // Update the block time to be that of the block in which the transaction is confirmed.
                if (block != null)
                {
                    spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.BlockTime.Ticks);
                }
            }
        }

        /// <summary>
        /// Adds a transaction that credits the wallet with new coins.
        /// This method is can be called many times for the same transaction (idempotent).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="utxo">The unspent output to add to the wallet.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="isPropagated">Propagation state of the transaction.</param>
        private void AddTransactionToWallet(Transaction transaction, TxOut utxo, int? blockHeight = null, MerkleBlock block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(utxo, nameof(utxo));


            uint256 transactionHash = transaction.GetHash();

            // Get the collection of transactions to add to.
            Script script = utxo.ScriptPubKey;
            this.keysLookup.TryGetValue(script, out HdAddress address);
            ICollection<TransactionData> addressTransactions = address.Transactions;

            // Check if a similar UTXO exists or not (same transaction ID and same index).
            // New UTXOs are added, existing ones are updated.
            int index = transaction.Outputs.IndexOf(utxo);
            Money amount = utxo.Value;
            TransactionData foundTransaction = addressTransactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
            if (foundTransaction == null)
            {
                this.logger.Information("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
                var newTransaction = new TransactionData
                {
                    Amount = amount,
                    IsCoinBase = transaction.IsCoinBase == false ? (bool?)null : true,
                    IsCoinStake =  false/*transaction.IsCoinStake == false ? (bool?)null : true*/,
                    BlockHeight = blockHeight,
                    //BlockHash = block?.GetHash(),
                    Id = transactionHash,
                    CreationTime = block.Header.BlockTime, //TODO: TIME
                    Index = index,
                    ScriptPubKey = script,
                    Hex = true /*this.walletSettings.SaveTransactionHex*/ ? transaction.ToHex() : null,
                    IsPropagated = isPropagated
                };

                // Add the Merkle proof to the (non-spending) transaction.
                if (block != null)
                {
                    //newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                    newTransaction.MerkleProof = block.PartialMerkleTree;
                }

                //TODO: Event - Add event to notify of new transaction added to wallet
                addressTransactions.Add(newTransaction);
                this.AddInputKeysLookupLocked(newTransaction);
            }
            else
            {
                //TODO: Event - Add event to notify updating of transaction in wallet
                this.logger.Information("Transaction ID '{0}' found, updating.", transactionHash);

                // Update the block height and block hash.
                if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
                {
                    foundTransaction.BlockHeight = blockHeight;
                    //foundTransaction.BlockHash = block?.GetHash();
                }

                // Update the block time.
                if (block != null)
                {
                    foundTransaction.CreationTime = block.Header.BlockTime;
                }

                // Add the Merkle proof now that the transaction is confirmed in a block.
                if ((block != null) && (foundTransaction.MerkleProof == null))
                {
                    //foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                    foundTransaction.MerkleProof = block.PartialMerkleTree;

                }

                if (isPropagated)
                    foundTransaction.IsPropagated = true;
            }

            this.TransactionFoundInternal(script);
        }


        public virtual void TransactionFoundInternal(Script script)
        {

                foreach (HdAccount account in Wallet.GetAccountsByCoinType(this.coinType))
                {
                    bool isChange;
                    if (account.ExternalAddresses.Any(address => address.P2WPKH_ScriptPubKey == script || address.P2PKH_ScriptPubKey == script || address.P2SH_P2WPKH_ScriptPubKey == script)) //Changed
                    {
                        isChange = false;
                    }
                    else if (account.InternalAddresses.Any(address => address.P2WPKH_ScriptPubKey == script || address.P2PKH_ScriptPubKey == script || address.P2SH_P2WPKH_ScriptPubKey == script)) //Changed
                    {
                        isChange = true;
                    }
                    else
                    {
                        continue;
                    }

                    IEnumerable<HdAddress> newAddresses = this.AddAddressesToMaintainBuffer(account, isChange);

                    this.UpdateKeysLookupLocked(newAddresses);
                }
            
        }

        private IEnumerable<HdAddress> AddAddressesToMaintainBuffer(HdAccount account, bool isChange)
        {
            HdAddress lastUsedAddress = account.GetLastUsedAddress(isChange);
            int lastUsedAddressIndex = lastUsedAddress?.Index ?? -1;
            int addressesCount = isChange ? account.InternalAddresses.Count() : account.ExternalAddresses.Count();
            int emptyAddressesCount = addressesCount - lastUsedAddressIndex - 1;
            int addressesToAdd = unusedAddressesBuffer - emptyAddressesCount;

            return addressesToAdd > 0 ? account.CreateAddresses(this.network, addressesToAdd, isChange) : new List<HdAddress>();
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
                this.logger.Information("Updating last block synced for wallet {walletName} to {height}", wallet.Name, chainedBlock.Height);
                wallet.SetLastBlockDetailsByCoinType(this.coinType, chainedBlock);
            }
        }




        /// <summary>
        /// Updates details of the last block synced in a wallet when the chain of headers finishes downloading.
        /// </summary>
        /// <param name="wallets">The wallets to update when the chain has downloaded.</param>
        /// <param name="date">The creation date of the block with which to update the wallet.</param>
        private void UpdateWhenChainDownloaded(Wallet wallet, DateTime date)
        {
            this.asyncLoopFactory.RunUntil("WalletManager.DownloadChain", new System.Threading.CancellationToken(),
                () => this.chain.IsDownloaded(),
                () =>
                {
                    int heightAtDate = this.chain.GetHeightAtTime(date);

                        this.UpdateLastBlockSyncedHeight(Wallet, this.chain.GetBlock(heightAtDate));
                        this.SaveWallet(wallet);
                },
                (ex) =>
                {
                    // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                    // sync from the current height.
                    
                        this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
                    
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
