// Not HD wallet, because of the following privacy reasons:
// This way if it is hacked, the transaction history will not leak out
// The structure is simple: every private key gets encrypted separately and goes to a new line

using System;
using System.Collections.Generic;
using System.IO;
using HiddenWallet.Helpers;
using NBitcoin;

namespace HiddenWallet.DataClasses
{
    internal class Wallet
    {
        private readonly Dictionary<string, string> _encryptedPrivateKeysByAddresses = new Dictionary<string, string>();
        private readonly Network _network;

        internal Wallet(string path, Network network, string password)
        {
            _network = network;

            if (File.Exists(path))
                Load(path, password);
            else
                Create(path, password);
        }

        /// <summary>
        /// Creates the wallet with one encrypted private key. It also loads it.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="password"></param>
        private void Create(string path, string password)
        {
            if (File.Exists(path)) throw new Exception("WalletAlreadyExists");

            var pathDirectory = Path.GetDirectoryName(path);
            if (pathDirectory != null)
                Directory.CreateDirectory(pathDirectory);
            else
                throw new Exception("WrongWalletPath");

            var encryptedPrivateKey = StringCipher.Encrypt(GeneratePrivateKey(), password);

            File.WriteAllText(path, encryptedPrivateKey);

            Load(path, password);
        }

        /// <summary>
        /// Loads the wallet from specified wallet path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="password"></param>
        private void Load(string path, string password)
        {
            if (!File.Exists(path)) throw new Exception("WalletDontExists");

            foreach (var encryptedPrivateKey in File.ReadAllLines(path))
            {
                var privateKey = new BitcoinSecret(StringCipher.Decrypt(encryptedPrivateKey, password));
                var address = privateKey.GetAddress();
                _encryptedPrivateKeysByAddresses.Add(encryptedPrivateKey, address.ToString());
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>Private key</returns>
        private string GeneratePrivateKey()
        {
            var key = new Key();
            var privateKey = key.GetBitcoinSecret(_network);
            return privateKey.ToString();
        }
    }
}