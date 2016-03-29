using System.Collections.Generic;
using System.IO;
using HiddenWallet.DataClasses;
using HiddenWallet.Properties;

namespace HiddenWallet.Services
{
    internal static class WalletServices
    {
        private static Wallet _wallet;

        internal static void CreateWallet(string password)
        {
            DataRepository.Main.Wallet = new Wallet(
                DataRepository.Main.PathWalletFile,
                DataRepository.Main.Network,
                password);
            _wallet = DataRepository.Main.Wallet;
        }

        internal static bool WalletExists()
        {
            return File.Exists(DataRepository.Main.PathWalletFile);
        }

        internal static string GetPathWalletFile()
        {
            return !WalletExists()
                ? Resources.Wallet_file_not_found
                : Path.GetFullPath(DataRepository.Main.PathWalletFile);
        }

        internal static string GenerateKey()
        {
            return _wallet.GenerateKey();
        }

        internal static HashSet<string> GetAddresses()
        {
            var addresses = new HashSet<string>();

            foreach (var address in _wallet.Addresses)
            {
                addresses.Add(address.ToString());
            }

            return addresses;
        }

        internal static decimal GetBalance()
        {
            return _wallet.Balance;
        }

        internal static void Sync()
        {
            _wallet.Sync();
        }
    }
}