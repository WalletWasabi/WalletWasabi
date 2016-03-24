// Every private key can be derived from the seed private key and an id.
// Every public key can be derived from the seed public key and an id.
// Structure:
//   File 1: seed private key in encrypted form
//   File 2: count of generated keys

using System;
using System.IO;
using System.Security.Cryptography;
using HiddenWallet.Helpers;
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

        public int KeyCount
        {
            get
            {
                if (!File.Exists(_pathKeyCountFile))
                    throw new Exception("_pathKeyCountFileDontExists");
                try
                {
                    var keyCount = File.ReadAllText(_pathKeyCountFile);
                    return int.Parse(keyCount);
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
        }
    }
}