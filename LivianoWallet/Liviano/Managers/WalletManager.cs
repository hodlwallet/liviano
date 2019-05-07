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
using Liviano.Managers;
using Liviano.Enums;
using Liviano.Exceptions;
using Liviano.Interfaces;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Liviano.Behaviors;

namespace Liviano.Managers
{
    public class WalletManager
    {
        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network _Network;

        private MessageHub _EventHub;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is restored.</summary>
        private const int _WalletRecoveryAccountsCount = 1;

        /// <summary>Quantity of accounts created in a wallet file when a wallet is created.</summary>
        private const int _WalletCreationAccountsCount = 1;

        /// <summary>The type of coin used in this manager.</summary>
        private readonly CoinType _CoinType;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider _DateTimeProvider;

        ///<summary>An object capable of storing and retreiving <see cref="_Wallet"/>s</summary>
        private readonly IStorageProvider _StorageProvider;

        /// <summary>
        /// A lock object that protects access to the <see cref="_Wallet"/>.
        /// Any of the collections inside Wallet must be synchronized using this lock.
        /// </summary>
        private readonly object _Lock;

        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
        // 2. the list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
        private Dictionary<OutPoint, TransactionData> _OutpointLookup;

        internal Dictionary<Script, HdAddress> _KeysLookup;

        /// <summary>The chain of headers.</summary>
        private readonly ConcurrentChain _Chain;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory _AsyncLoopFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger _Logger;

        /// <summary>
        /// Size of the buffer of unused addresses maintained in an account.
        /// </summary>
        private const int _UnusedAddressesBuffer = 20;

        /// <summary>The settings for the wallet feature.</summary>
        private readonly IScriptAddressReader _ScriptAddressReader;

        private readonly double _WalletSavetimeIntervalInMinutes = 1;

        private IAsyncLoop _AsyncLoop;

        /// <summary>Gets the wallet.</summary>
        private Wallet _Wallet { get; set; }

        public uint256 WalletTipHash { get; set; }

        public Network Network { get { return _Network; } }

        public event EventHandler<TransactionData> OnNewSpendingTransaction;

        public event EventHandler<TransactionData> OnUpdateSpendingTransaction;

        public event EventHandler<TransactionData> OnNewTransaction;

        public event EventHandler<TransactionData> OnUpdateTransaction;

        public WalletManager(ILogger logger, Network network, ConcurrentChain chain, IAsyncLoopFactory asyncLoopFactory, IDateTimeProvider dateTimeProvider, IScriptAddressReader scriptAddressReader, IStorageProvider storageProvider)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(scriptAddressReader, nameof(scriptAddressReader));
            Guard.NotNull(storageProvider, nameof(storageProvider));

            _Lock = new object();

            _Logger = logger;

            _Network = network;
            _CoinType = CoinType.Bitcoin;
            _AsyncLoopFactory = asyncLoopFactory;
            _Chain = chain;
            _ScriptAddressReader = scriptAddressReader;
            _DateTimeProvider = dateTimeProvider;
            _StorageProvider = storageProvider;
            _KeysLookup = new Dictionary<Script, HdAddress>();
            _OutpointLookup = new Dictionary<OutPoint, TransactionData>();

            _EventHub = MessageHub.Instance;
            _EventHub.Subscribe<TransactionBroadcastEntry>(HandleBroadcastedTransaction);
        }

        public WalletManager(ILogger logger, Network network, string walletId = null, ConcurrentChain chain = null)
        {
            walletId = walletId ?? Guid.NewGuid().ToString();
            chain = chain ?? new ConcurrentChain();

            _Lock = new object();

            _Logger = logger;
            _Network = network;
            _Chain = chain;

            _ScriptAddressReader = new ScriptAddressReader();
            _DateTimeProvider = new DateTimeProvider();
            _StorageProvider = new FileSystemStorageProvider(walletId);
            _KeysLookup = new Dictionary<Script, HdAddress>();
            _OutpointLookup = new Dictionary<OutPoint, TransactionData>();

            _EventHub = MessageHub.Instance;
            _EventHub.Subscribe<TransactionBroadcastEntry>(HandleBroadcastedTransaction);
        }

        /// <summary>
        /// Method to handle <see cref="TransactionBroadcastEntry"/>s as they are published onto the eventhub
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
                _Logger.Error("Exception occurred: {errorMessage}", transactionEntry.ErrorMessage);
            }
        }

        public void Start()
        {
            // Find wallets and load them in memory.
            _Wallet = _StorageProvider.LoadWallet();

            foreach (HdAccount account in _Wallet.GetAccountsByCoinType(_CoinType))
            {
                AddAddressesToMaintainBuffer(account, false);
                AddAddressesToMaintainBuffer(account, true);
            }

            // Load data in memory for faster lookups.
            LoadKeysLookupLock();

            // Find the last chain block received by the wallet manager.
            WalletTipHash = _Chain.Tip.HashBlock;

            // Save the wallets file every 5 minutes to help against crashes.
            _AsyncLoop = _AsyncLoopFactory.Run("Wallet persist job", token =>
                {
                    SaveWallet(_Wallet);
                    _Logger.Information("Wallets saved to file at {0}.", _DateTimeProvider.GetUtcNow());

                    _Logger.Information("(-)[IN_ASYNC_LOOP]");

                    return Task.CompletedTask;
                },
                repeatEvery: TimeSpan.FromMinutes(_WalletSavetimeIntervalInMinutes),
                startAfter: TimeSpan.FromMinutes(_WalletSavetimeIntervalInMinutes)
            );
        }

        public IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(CoinType bitcoin, int height, int confirmations = 0)
        {
            return _Wallet.GetAllSpendableTransactions(bitcoin, height, confirmations);
        }

        public IEnumerable<TransactionData> GetAllTransactionsByCoinType(CoinType bitcoin)
        {
            return _Wallet.GetAllTransactionsByCoinType(bitcoin);
        }

        public IEnumerable<HdAddress> GetAllAddressesByCoinType(CoinType bitcoin)
        {
            return _Wallet.GetAllAddressesByCoinType(bitcoin);
        }

        public IEnumerable<HdAccount> GetAllAccountsByCoinType(CoinType bitcoin)
        {
            return _Wallet.GetAccountsByCoinType(bitcoin);
        }

        public uint256 LastReceivedBlockHash()
        {
            if (_Wallet == null)
            {
                throw new InvalidOperationException();
            }

            uint256 lastBlockSyncedHash;
            lock (this._Lock)
            {
                lastBlockSyncedHash = _Wallet.AccountsRoot
                    .Where(x => x.CoinType == _CoinType)
                    .Where(w => w != null)
                    .OrderBy(o => o.LastBlockSyncedHeight)
                    .FirstOrDefault()?.LastBlockSyncedHash;

                var height = _Wallet.AccountsRoot.Select(x => x.LastBlockSyncedHeight).FirstOrDefault();

                if (lastBlockSyncedHash == null && height.HasValue)
                {
                    if (_Chain.Tip.Height >= height.Value)
                    {
                        return _Chain.GetBlock(height.Value).HashBlock;
                    }
                    else
                    {
                        _Logger.Warning("Tip of saved chain is {tipofChain}, last synced height is {lastSyncedHeight}.\nRolling last synced height to chain tip ",_Chain.Tip.Height,height.Value);
                        _Wallet.SetLastBlockDetailsByCoinType(_CoinType, _Chain.Tip);

                        return  _Wallet.AccountsRoot.Select(x => x.LastBlockSyncedHash).FirstOrDefault();
                    }
                }

                //If details about the last block synced are not present in the wallet,
                //find out which is the oldest wallet and set the last block synced to be the one at this date.
                if (lastBlockSyncedHash == null)
                {
                   lastBlockSyncedHash = _Chain.Tip.HashBlock;
                }

                if (!_Chain.Contains(lastBlockSyncedHash))
                {
                    _Wallet.SetLastBlockDetailsByCoinType(CoinType.Bitcoin, _Chain.Tip);
                    return _Wallet.AccountsRoot.Select(x => x.LastBlockSyncedHash).FirstOrDefault();

                }
            }

            return lastBlockSyncedHash;
        }


        /// <summary>
        /// Loads the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void LoadKeysLookupLock()
        {
            lock (this._Lock)
            {

                    IEnumerable<HdAddress> addresses = _Wallet.GetAllAddressesByCoinType(_CoinType);
                    foreach (HdAddress address in addresses)
                    {
                        this._KeysLookup[address.ScriptPubKey] = address;

                        foreach (TransactionData transaction in address.Transactions)
                        {
                            this._OutpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                        }
                    }
            }
        }

        /// <inheritdoc />
        public Mnemonic CreateWallet(string name, string password = "", Mnemonic mnemonic = null, string wordlist = "english", int wordCount = 12)
        {
            Guard.NotEmpty(name, nameof(name));
            Guard.NotNull(password, nameof(password));

            if (_StorageProvider.WalletExists())
            {
                _Logger.Error("Cannot create wallet because with name {name} it already exists, please load the wallet", name);

                throw new WalletException($"Wallet with name {name} already exists");
            }

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(HdOperations.WordlistFromString(wordlist), HdOperations.WordCountFromInt(wordCount));

            ExtKey extendedKey = HdOperations.GetExtendedKey(mnemonic);

            // Create a wallet file
            string encryptedSeed;
            encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, _Network).ToWif();

            Wallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode);

            // Generate multiple accounts and addresses from the get-go.
            for (int i = 0; i < _WalletCreationAccountsCount; i++)
            {
                HdAccount account = wallet.AddNewAccount(_CoinType, _DateTimeProvider.GetTimeOffset(), password);
                IEnumerable<HdAddress> newReceivingAddresses = account.CreateAddresses(this._Network, _UnusedAddressesBuffer);
                IEnumerable<HdAddress> newChangeAddresses = account.CreateAddresses(this._Network, _UnusedAddressesBuffer, true);
                this.UpdateKeysLookupLocked(newReceivingAddresses.Concat(newChangeAddresses));
            }

            // The creation date of the wallet.
            wallet.CreationTime = _DateTimeProvider.GetTimeOffset();

            // Save the changes to the file and add addresses to be tracked.
            this.SaveWallet(wallet);
            this.Load(wallet);

            return mnemonic;
        }

        /// <inheritdoc />
        public void SaveWallet(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            lock (this._Lock)
            {
                //Save the wallet
                this._Logger.Information("Saving wallet {walletName} to storage provider", wallet.Name);
                this._StorageProvider.SaveWallet(wallet);
            }
        }
        

        public void SaveWallet()
        {
            SaveWallet(_Wallet);
        }

        public void UnloadWallet()
        {
            if (_Wallet == null)
            {
                _Logger.Information("Wallet hasn't been loaded");
                return;
            }

            _Logger.Information("{walletName} unloaded on WalletManager", _Wallet.Name);
            _Wallet = null;
        }

        public bool LoadWallet(string password = "")
        {
            if (!_StorageProvider.WalletExists())
            {
                return false;
            }

            // Load the the wallet.
            Wallet wallet = this._StorageProvider.LoadWallet();

            // Check the password.
            try
            {
                
                if (!wallet.IsExtPubKeyWallet)
                    HdOperations.DecryptSeed(wallet.EncryptedSeed, wallet.Network, password);
            }
            catch (Exception ex)
            {
                _Logger.Error("Exception occurred: {0}", ex.ToString());
                _Logger.Error("(-)[EXCEPTION]");

                throw new SecurityException(ex.Message);
            }

            _Logger.Information("{walletName} file loaded", wallet.Name);

            Load(wallet);

            return true;
        }

        /// <summary>
        /// Loads the wallet to be used by the manager.
        /// </summary>
        /// <param name="wallet">The wallet to load.</param>
        private void Load(Wallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            _Logger.Information("{walletName} loaded WalletManager", wallet.Name);
            _Wallet = wallet;
        }

        public IEnumerable<AccountBalance> GetBalances(string accountName = null)
        {
            var balances = new List<AccountBalance>();

            lock (this._Lock)
            {

                var accounts = new List<HdAccount>();
                if (!string.IsNullOrEmpty(accountName))
                {
                    accounts.Add(_Wallet.GetAccountByCoinType(accountName, this._CoinType));
                }
                else
                {
                    accounts.AddRange(_Wallet.GetAccountsByCoinType(this._CoinType));
                }

                foreach (HdAccount account in accounts)
                {
                    (Money amountConfirmed, Money amountUnconfirmed) result = account.GetSpendableAmount();

                    balances.Add(new AccountBalance
                    {
                        Account = account,
                        AmountConfirmed = result.amountConfirmed,
                        AmountUnconfirmed = result.amountUnconfirmed
                    });
                }
            }

            return balances;
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

            if (_Wallet != null) //On start wallet is null
            {
                if (string.Equals(this._Wallet.Name, name, StringComparison.OrdinalIgnoreCase))
                    throw new WalletException($"Wallet with name '{name}' already exists.");

                if (this._Wallet.EncryptedSeed != encryptedSeed)
                    throw new WalletException("Cannot create this wallet as a wallet with the same private key already exists. If you want to restore your wallet make sure you have your mnemonic and your password handy!");

            }

            var walletFile = new Wallet
            {
                Name = name,
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                CreationTime = creationTime ?? _DateTimeProvider.GetTimeOffset(),
                Network = _Network,
                AccountsRoot = new List<AccountRoot> { new AccountRoot(_CoinType, new List<HdAccount>()){}},
            };

            this._Logger.Information("Wallet file created for wallet {walletName}", walletFile.Name);

            // Create a folder if none exists and persist the file.
            this.SaveWallet(walletFile);

            return walletFile;
        }

        /// <summary>
        /// Update the keys and transactions we're tracking in memory for faster lookups.
        /// </summary>
        public void UpdateKeysLookupLocked(IEnumerable<HdAddress> addresses)
        {
            if (addresses == null || !addresses.Any()) return;

            lock (this._Lock)
            {
                foreach (HdAddress address in addresses)
                    this._KeysLookup[address.ScriptPubKey] = address;
            }
        }

        private void AddInputKeysLookupLocked(TransactionData transactionData)
        {
            Guard.NotNull(transactionData, nameof(transactionData));

            lock (this._Lock)
            {
                this._OutpointLookup[new OutPoint(transactionData.Id, transactionData.Index)] = transactionData;
            }
        }


        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, MerkleBlock block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();

            bool foundReceivingTrx = false, foundSendingTrx = false;

            lock (this._Lock)
            {
                // Check the outputs.
                foreach (TxOut utxo in transaction.Outputs)
                {
                    // Check if the outputs contain one of our addresses.
                    if (this._KeysLookup.TryGetValue(utxo.ScriptPubKey, out HdAddress _))
                    {
                        this.AddTransactionToWallet(transaction, utxo, blockHeight, block, isPropagated);
                        foundReceivingTrx = true;
                    }
                }

                // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
                foreach (TxIn input in transaction.Inputs)
                {
                    if (!this._OutpointLookup.TryGetValue(input.PrevOut, out TransactionData tTx))
                    {
                        continue;
                    }

                    // Get the details of the outputs paid out.
                    IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
                    {
                        // If script is empty ignore it.
                        if (o.Value == (long) 0)
                            return false;

                        // Check if the destination script is one of the wallet's.
                        bool found = this._KeysLookup.TryGetValue(o.ScriptPubKey, out HdAddress addr);

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
                    this.SaveWallet(_Wallet);
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
            TransactionData spentTransaction = _KeysLookup.Values.Distinct().SelectMany(v => v.Transactions)
                .SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
            if (spentTransaction == null)
            {
                // Strange, why would it be null?
                _Logger.Information("(-)[TX_NULL]");
                
                return;
            }

            // If the details of this spending transaction are seen for the first time.
            if (spentTransaction.SpendingDetails == null)
            {
                _Logger.Information("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

                var payments = new List<PaymentDetails>();
                foreach (TxOut paidToOutput in paidToOutputs)
                {
                    // Figure out how to retrieve the destination address.
                    string destinationAddress = _ScriptAddressReader.GetAddressFromScriptPubKey(_Network, paidToOutput.ScriptPubKey);
                    if (destinationAddress == string.Empty)
                        if (_KeysLookup.TryGetValue(paidToOutput.ScriptPubKey, out HdAddress destination))
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
                    CreationTime = block != null ? block.Header.BlockTime : new DateTimeOffset(DateTime.Now),
                    BlockHeight = blockHeight,
                    Hex = true ? transaction.ToHex() : null,
                    IsCoinStake = false
                };

                spentTransaction.SpendingDetails = spendingDetails;

                if (block != null)
                {
                    spentTransaction.MerkleProof = block.PartialMerkleTree;
                }

                OnNewSpendingTransaction?.Invoke(this, spentTransaction);
            }
            else // If this spending transaction is being confirmed in a block.
            {
                _Logger.Information("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

                // Update the block height.
                if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
                {
                    spentTransaction.SpendingDetails.BlockHeight = blockHeight;
                }

                // Update the block time to be that of the block in which the transaction is confirmed.
                if (block != null)
                {
                    spentTransaction.SpendingDetails.CreationTime = block.Header.BlockTime;
                }

                OnUpdateSpendingTransaction?.Invoke(this, spentTransaction);
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
            _KeysLookup.TryGetValue(script, out HdAddress address);
            ICollection<TransactionData> addressTransactions = address.Transactions;

            // Check if a similar UTXO exists or not (same transaction ID and same index).
            // New UTXOs are added, existing ones are updated.
            int index = transaction.Outputs.IndexOf(utxo);
            Money amount = utxo.Value;
            TransactionData foundTransaction = addressTransactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
            if (foundTransaction == null)
            {
                _Logger.Information("UTXO '{0}-{1}' not found, creating.", transactionHash, index);

                var newTransaction = new TransactionData
                {
                    Amount = amount,
                    IsCoinBase = transaction.IsCoinBase == false ? (bool?)null : true,
                    IsCoinStake = false/*transaction.IsCoinStake == false ? (bool?)null : true*/,
                    BlockHeight = blockHeight,
                    //BlockHash = block?.GetHash(),
                    Id = transactionHash,
                    CreationTime = block != null ? block.Header.BlockTime : new DateTimeOffset(DateTime.Now), //TODO: TIME
                    Index = index,
                    ScriptPubKey = script,
                    Hex = true /*this.walletSettings.SaveTransactionHex*/ ? transaction.ToHex() : null,
                    IsPropagated = isPropagated,
                    IsSend = address.IsChangeAddress(),
                    IsReceive = !address.IsChangeAddress()
                };

                // Add the Merkle proof to the (non-spending) transaction.
                if (block != null)
                {
                    //newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                    newTransaction.MerkleProof = block.PartialMerkleTree;
                }

                addressTransactions.Add(newTransaction);
                AddInputKeysLookupLocked(newTransaction);

                OnNewTransaction?.Invoke(this, newTransaction);
            }
            else
            {
                this._Logger.Information("Transaction ID '{0}' found, updating.", transactionHash);

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

                OnUpdateTransaction?.Invoke(this, foundTransaction);
            }

            this.TransactionFoundInternal(script);
        }

        public virtual void TransactionFoundInternal(Script script)
        {

                foreach (HdAccount account in _Wallet.GetAccountsByCoinType(this._CoinType))
                {
                    bool isChange;
                    if (account.ExternalAddresses.Any(address => address.ScriptPubKey == script))
                    {
                        isChange = false;
                    }
                    else if (account.InternalAddresses.Any(address => address.ScriptPubKey == script))
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
            int addressesToAdd = _UnusedAddressesBuffer - emptyAddressesCount;

            return addressesToAdd > 0 ? account.CreateAddresses(this._Network, addressesToAdd, isChange) : new List<HdAddress>();
        }

        /// <inheritdoc />
        public void UpdateLastBlockSyncedHeight(ChainedBlock chainedBlock)
        {
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            // The block locator will help when the wallet
            // needs to rewind this will be used to find the fork.
            _Wallet.BlockLocator = GetPartialLocator(chainedBlock).Blocks;

            lock (this._Lock)
            {
                this._Logger.Information("Updating last block synced for wallet {walletName} to {height}",_Wallet.Name, chainedBlock.Height);
                _Wallet.SetLastBlockDetailsByCoinType(this._CoinType, chainedBlock);
            }
        }


        private BlockLocator GetPartialLocator(ChainedBlock block)
        {
            //TODO: tries to create block locator all the way to genesis, we dont need all that, we just need the last locator, techincally.
            //But we can create an exponential locator the first block we do have.
            int nStep = 1;
            List<uint256> vHave = new List<uint256>();

            var pindex = block;
            while (pindex != null)
            {
                vHave.Add(pindex.HashBlock);
                // Stop when we have added the genesis block.
                if (pindex.Height == 0)
                    break;
                // Exponentially larger steps back, plus the genesis block.
                int nHeight = Math.Max(pindex.Height - nStep, 0);
                while (pindex.Height > nHeight)
                {
                    pindex = pindex.Previous;

                    if (pindex == null)
                    {
                        break;
                    }
                }
                if (vHave.Count > 10)
                    nStep *= 2;
            }

            var locators = new BlockLocator();
            locators.Blocks = vHave;
            return locators;
        }
        /// <summary>
        /// Updates details of the last block synced in a wallet when the chain of headers finishes downloading.
        /// </summary>
        /// <param name="wallets">The wallets to update when the chain has downloaded.</param>
        /// <param name="date">The creation date of the block with which to update the wallet.</param>
        //private void UpdateWhenChainDownloaded(Wallet wallet, DateTime date)
        //{
        //    this._AsyncLoopFactory.RunUntil("WalletManager.DownloadChain", new System.Threading.CancellationToken(),
        //        () => this._Chain.IsDownloaded(),
        //        () =>
        //        {
        //            int heightAtDate = this._Chain.GetHeightAtTime(date);

        //                this.UpdateLastBlockSyncedHeight(this._Chain.GetBlock(heightAtDate));
        //                this.SaveWallet(wallet);
        //        },
        //        (ex) =>
        //        {
        //            // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
        //            // sync from the current height.

        //                this.UpdateLastBlockSyncedHeight(this._Chain.Tip);

        //        },
        //        TimeSpans.FiveSeconds);
        //}

        /// <inheritdoc />
        public IEnumerable<AccountHistory> GetHistory(string accountName = null)
        {
            var accountsHistory = new List<AccountHistory>();

            lock (this._Lock)
            {
                var accounts = new List<HdAccount>();
                if (!string.IsNullOrEmpty(accountName))
                {
                    accounts.Add(_Wallet.GetAccountByCoinType(accountName, this._CoinType));
                }
                else
                {
                    accounts.AddRange(_Wallet.GetAccountsByCoinType(this._CoinType));
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
            lock (this._Lock)
            {
                // Get transactions contained in the account.
                items = account.GetCombinedAddresses()
                    .Where(a => a.Transactions.Any())
                    .SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToArray();
            }

            return new AccountHistory { Account = account, History = items };
        }

        /// <inheritdoc />
        public IEnumerable<TransactionData> RemoveAllTransactions()
        {
            var removedTransactions = new List<TransactionData>();

            lock (this._Lock)
            {
                IEnumerable<HdAccount> accounts = _Wallet.GetAccountsByCoinType(this._CoinType);
                foreach (HdAccount account in accounts)
                {
                    foreach (HdAddress address in account.GetCombinedAddresses())
                    {
                        removedTransactions.Union(address.Transactions);
                        address.Transactions.Clear();
                    }
                }
            }

            if (removedTransactions.Any())
            {
                this.SaveWallet(_Wallet);
            }

            return removedTransactions;
        }

        /// <inheritdoc />
        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIdsLocked(IEnumerable<uint256> transactionsIds)
        {
            Guard.NotNull(transactionsIds, nameof(transactionsIds));

            List<uint256> idsToRemove = transactionsIds.ToList();

            var result = new HashSet<(uint256, DateTimeOffset)>();

            lock (this._Lock)
            {
                IEnumerable<HdAccount> accounts = _Wallet.GetAccountsByCoinType(this._CoinType);
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
                this.SaveWallet(_Wallet);
            }

            return result;
        }

        /// <inheritdoc />
        public DateTimeOffset GetWalletCreationTime()
        {
            return _Wallet.CreationTime;
        }

        /// <inheritdoc />
        public ICollection<uint256> GetWalletBlockLocator()
        {
            return _Wallet.BlockLocator;
        }

        /// <inheritdoc />
        public int? GetWalletHeight()
        {
            return _Wallet.AccountsRoot.Min().LastBlockSyncedHeight;
        }

        /// <inheritdoc />
        public Wallet GetWallet()
        {
            return _Wallet;
        }

        public IStorageProvider GetStorageProvider()
        {
            return _StorageProvider;
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
    }
}
