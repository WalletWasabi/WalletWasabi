// Every private key can be derived from the seed private key and an id.
// Every public key can be derived from the seed public key and an id.
// Structure:
//   File 1: seed private key in encrypted form
//   File 2: count of generated keys

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HiddenWallet.Helpers;
using HiddenWallet.Helpers.Wrappers;
using NBitcoin;

namespace HiddenWallet.DataClasses
{
    internal class Wallet
    {
        private readonly Network _network;
        private readonly string _pathKeyCountFile;
        private readonly string _pathWalletDirectory;
        private readonly string _pathWalletFile;
        private ExtPubKey _seedPublicKey;

        #region UpdateableMembers
        internal BindingList<StringValue> NotUsedAddresses = new BindingList<StringValue>();
        internal decimal Balance;
        #endregion

        internal Wallet(string pathWalletFile, Network network, string password)
        {
            _network = network;
            _pathWalletFile = pathWalletFile;
            _pathWalletDirectory = Path.GetDirectoryName(_pathWalletFile);
            if (_pathWalletDirectory == null)
                throw new Exception("_pathWalletDirectoryIsNull");
            _pathKeyCountFile = Path.Combine(_pathWalletDirectory, @"KeyCount.txt");

            if (!File.Exists(_pathWalletFile))
                Create(password);

            Load(password);
        }

        internal uint KeyCount
        {
            get
            {
                if (!File.Exists(_pathKeyCountFile))
                    throw new Exception("_pathKeyCountFileDontExists");
                try
                {
                    var keyCount = File.ReadAllText(_pathKeyCountFile);
                    return UInt32.Parse(keyCount);
                }
                catch (Exception)
                {
                    throw new Exception("WrongKeyCountFileFormat");
                }
            }
            private set
            {
                if (value != 0 && !File.Exists(_pathKeyCountFile))
                    throw new Exception("_pathKeyCountFileDontExists");

                File.WriteAllText(_pathKeyCountFile, value.ToString());
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

        /// <summary>
        ///     Creates new wallet file with one encrypted wif seed.
        /// </summary>
        /// <param name="password"></param>
        private void Create(string password)
        {
            var seedPrivateKey = new ExtKey();
            var seedWif = seedPrivateKey.GetWif(_network);
            var encryptedSeedWif = StringCipher.Encrypt(seedWif.ToString(), password);

            Directory.CreateDirectory(_pathWalletDirectory);
            File.WriteAllText(_pathWalletFile, encryptedSeedWif);

            if (File.Exists(_pathKeyCountFile))
                throw new Exception("_pathKeyCountFileExists");
            KeyCount = 0;
        }

        /// <summary>
        ///     Loads the wallet.
        /// </summary>
        /// <param name="password"></param>
        private void Load(string password)
        {
            if (!File.Exists(_pathWalletFile))
                throw new Exception("_pathWalletFileDontExists");

            try
            {
                var encryptedSeedWif = File.ReadAllText(_pathWalletFile);
                var seedWifString = StringCipher.Decrypt(encryptedSeedWif, password);
                var bitcoinSeedPrivateKey = new BitcoinExtKey(seedWifString);
                var seedPrivateKey = bitcoinSeedPrivateKey.ExtKey;
                _seedPublicKey = seedPrivateKey.Neuter();
            }
            catch (CryptographicException)
            {
                throw new Exception("WrongPassword");
            }
            catch (Exception)
            {
                throw new Exception("WrongWalletFileFormat");
            }

            Update();
        }

        internal void Update()
        {
            decimal balance = 0;
            const int maxQueryable = 20;
            var addressesArray = Addresses.ToArray();

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
                        NotUsedAddresses.Add(new StringValue(addressInfo.Address));
                    }
                }
            }

            Balance = balance;
        }

        internal string GenerateKey()
        {
            KeyCount++;

            var address = _seedPublicKey.Derive(KeyCount).PubKey.GetAddress(_network).ToString();

            NotUsedAddresses.Add(new StringValue(address));

            return address;
        }
    }
}