using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HiddenWallet.Helpers;
using HiddenWallet.Helpers.Wrappers;
using NBitcoin;
using Newtonsoft.Json;

namespace HiddenWallet.DataClasses
{
    internal class Wallet
    {
        private readonly Network _network;
        private readonly string _pathWalletDirectory;
        private readonly string _pathWalletFile;
        private ExtKey _seedPrivateKey;
        private ExtPubKey _seedPublicKey;

        internal Wallet(string pathWalletFile, Network network, string password)
        {
            _network = network;
            _pathWalletFile = pathWalletFile;
            _pathWalletDirectory = Path.GetDirectoryName(_pathWalletFile);
            if (_pathWalletDirectory == null)
                throw new Exception("_pathWalletDirectoryIsNull");

            if (!File.Exists(_pathWalletFile))
                Create(password);

            LoadNoSync(password);
        }

        internal uint KeyCount
        {
            get
            {
                if (!File.Exists(_pathWalletFile))
                    throw new Exception("_pathWalletFileDontExists");
                try
                {
                    var walletContentString = File.ReadAllText(_pathWalletFile);
                    DataRepository.Main.WalletFileContent =
                        JsonConvert.DeserializeObject<Main.WalletFileStructure>(walletContentString);
                    var keyCount = DataRepository.Main.WalletFileContent.KeyCount;
                    return UInt32.Parse(keyCount);
                }
                catch (Exception)
                {
                    throw new Exception("WrongKeyCountFileFormat");
                }
            }
            private set
            {
                if (value != 0 && !File.Exists(_pathWalletFile))
                    throw new Exception("_pathWalletFileDontExists");

                DataRepository.Main.WalletFileContent.KeyCount = value.ToString();
                File.WriteAllText(_pathWalletFile, JsonConvert.SerializeObject(DataRepository.Main.WalletFileContent));
            }
        }

        internal HashSet<BitcoinAddress> Addresses
        {
            get
            {
                var addresses = new HashSet<BitcoinAddress>();

                for (uint i = 0; i <= KeyCount; i++)
                {
                    addresses.Add(_seedPublicKey.Derive(i).PubKey.GetAddress(_network));
                }
                return addresses;
            }
        }

        internal HashSet<BitcoinSecret> Secrets
        {
            get
            {
                var secrets = new HashSet<BitcoinSecret>();

                for (uint i = 0; i <= KeyCount; i++)
                {
                    secrets.Add(_seedPrivateKey.Derive(i).PrivateKey.GetBitcoinSecret(_network));
                }
                return secrets;
            }
        }

        /// <summary>
        ///     Creates new wallet file with one encrypted wif seed.
        /// </summary>
        /// <param name="password"></param>
        private void Create(string password)
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);

            var encryptedSeedWif = StringCipher.Encrypt(mnemonic.ToString(), password);

            Directory.CreateDirectory(_pathWalletDirectory);

            const int keyCount = 0;
            DataRepository.Main.WalletFileContent = new Main.WalletFileStructure(encryptedSeedWif, keyCount.ToString(),
                _network == Network.Main ? "MainNet" : "TestNet");

            DataRepository.Main.WalletFileContent.Save(_pathWalletFile);

            KeyCount = keyCount;
        }

        /// <summary>
        ///     Loads the wallet, but does not sync it.
        /// </summary>
        /// <param name="password"></param>
        private void LoadNoSync(string password)
        {
            if (!File.Exists(_pathWalletFile))
                throw new Exception("_pathWalletFileDontExists");

            try
            {
                DataRepository.Main.WalletFileContent = new Main.WalletFileStructure(_pathWalletFile);
                var mnemonicString = StringCipher.Decrypt(DataRepository.Main.WalletFileContent.Seed, password);
                var mnemonic = new Mnemonic(mnemonicString);

                _seedPrivateKey = mnemonic.DeriveExtKey(password);
                _seedPublicKey = _seedPrivateKey.Neuter();
            }
            catch (CryptographicException)
            {
                throw new Exception("WrongPassword");
            }
            catch (Exception)
            {
                throw new Exception("WrongWalletFileFormat");
            }
        }

        internal void Sync()
        {
            decimal balance = 0;
            const int maxQueryable = 20;
            var addressesArray = Addresses.ToArray();
            var notUsedAddressesHashSet = new HashSet<BindingAddress>();
            var unspentTransactions = new HashSet<TransactionInfo>();

            for (var i = 0; i <= Addresses.Count/maxQueryable; i++)
            {
                var addressesChunk = new HashSet<string>();
                for (var j = 0; j < maxQueryable; j++)
                {
                    var currentIndex = i*maxQueryable + j;
                    if (currentIndex >= Addresses.Count) break;
                    addressesChunk.Add(addressesArray[currentIndex].ToString());
                }

                var addressesResult = BlockrApi.GetAddressInfosSync(addressesChunk);

                foreach (var addressInfo in addressesResult)
                {
                    balance += addressInfo.TotalBalance;

                    if (addressInfo.TransactionCount == 0)
                    {
                        notUsedAddressesHashSet.Add(new BindingAddress(addressInfo.Address));
                    }

                    if (addressInfo.UnspentTransactions.Count > 0)
                    {
                        foreach (var transaction in addressInfo.UnspentTransactions)
                        {
                            unspentTransactions.Add(transaction);
                        }
                    }
                }
            }
            NotUsedAddresses.Clear();
            foreach (var bindingAddress in notUsedAddressesHashSet)
            {
                NotUsedAddresses.Add(bindingAddress);
            }

            UnspentTransactions.Clear();
            foreach (var transaction in unspentTransactions)
            {
                UnspentTransactions.Add(transaction);
            }

            Balance = balance;
        }

        /// <summary>
        ///     Generate new Key and returns it's Bitcoin address.
        /// </summary>
        /// <returns></returns>
        internal string GenerateKey()
        {
            KeyCount++;

            var address = _seedPublicKey.Derive(KeyCount).PubKey.GetAddress(_network).ToString();

            NotUsedAddresses.Add(new BindingAddress(address));

            return address;
        }

        internal void Send(string address, decimal amount)
        {
            if (UnspentTransactions.Count == 0)
                throw new Exception("NoUnspentTransactions");

            if (amount > Balance)
                throw new Exception("NotEnoughFunds");

            var blockr = new BlockrTransactionRepository(_network);
            var fundingTransactionCandidates = new Dictionary<Transaction, TransactionInfo>();
            foreach (var transactionInfo in UnspentTransactions)
            {
                var transaction = blockr.Get(transactionInfo.Hash);
                fundingTransactionCandidates.Add(transaction, transactionInfo);
            }

            var fee = CalculateFee();
            var fundingTransactions = new HashSet<Transaction>();
            var fundingTransactionInfos = new HashSet<TransactionInfo>();

            decimal transactionSum = 0;
            transactionSum -= fee;

            foreach (var candidate in fundingTransactionCandidates)
            {
                fundingTransactions.Add(candidate.Key);
                fundingTransactionInfos.Add(candidate.Value);

                transactionSum += candidate.Value.Amount;

                if (transactionSum >= amount)
                    break;
            }

            var payment = new Transaction();

            foreach (var fundingTransaction in fundingTransactions)
            {
                payment.Inputs.Add(new TxIn
                {
                    PrevOut = new OutPoint(fundingTransaction.GetHash(), 1),
                    ScriptSig = fundingTransaction.Outputs[1].ScriptPubKey // TODO: Figure out why outputs[1]
                });
            }

            var paymentAddress = new BitcoinPubKeyAddress(address);
            payment.Outputs.Add(new TxOut
            {
                Value = Money.Coins(amount),
                ScriptPubKey = paymentAddress.ScriptPubKey
            });

            var changeAddress = new BitcoinPubKeyAddress(GenerateKey());
            payment.Outputs.Add(new TxOut
            {
                Value = Money.Coins(transactionSum - amount),
                ScriptPubKey = changeAddress.ScriptPubKey
            });

            foreach (var fundingTransactionInfo in fundingTransactionInfos)
            {
                foreach (var secret in Secrets)
                {
                    if (secret.PubKey.GetAddress(_network).ToString() == fundingTransactionInfo.Address)
                    {
                        payment.Sign(secret, false);
                    }
                }
            }

            BlockrApi.PushTransactionSync(payment.GetHash().ToString());
        }

        internal decimal CalculateFee()
        {
            return 0; //throw new NotImplementedException();
        }

        #region MembersToSync

        internal BindingList<BindingAddress> NotUsedAddresses = new BindingList<BindingAddress>();
        internal HashSet<TransactionInfo> UnspentTransactions = new HashSet<TransactionInfo>();

        internal delegate void EventHandler(object sender, EventArgs args);

        internal event EventHandler ThrowEvent = delegate { };

        private decimal _balance;

        internal decimal Balance
        {
            get { return _balance; }
            set
            {
                _balance = value;
                BalanceChanged();
            }
        }

        internal void BalanceChanged()
        {
            ThrowEvent(this, new EventArgs());
        }

        #endregion
    }
}