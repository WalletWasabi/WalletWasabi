using System.Collections.Generic;
using System.IO;
using HiddenWallet.DataClasses;
using HiddenWallet.Properties;

namespace HiddenWallet.Services
{
    internal static class WalletServices
    {
        internal static void CreateWallet(string password)
        {
            DataRepository.Main.Wallet = new Wallet(
                Settings.Default.WalletFilePath,
                DataRepository.Main.Network,
                password);
        }

        internal static void LoadWallet(string password)
        {
            CreateWallet(password);
        }

        internal static bool WalletExists()
        {
            return File.Exists(Settings.Default.WalletFilePath);
        }

        internal static string GetPathWalletFile()
        {
            return !WalletExists()
                ? Resources.Wallet_file_not_found
                : Path.GetFullPath(Settings.Default.WalletFilePath);
        }

        internal static string GenerateKey()
        {
            return DataRepository.Main.Wallet.GenerateKey();
        }

        internal static HashSet<string> GetAddresses()
        {
            var addresses = new HashSet<string>();

            foreach (var address in DataRepository.Main.Wallet.Addresses)
            {
                addresses.Add(address.ToString());
            }

            return addresses;
        }

        internal static decimal GetBalance()
        {
            return DataRepository.Main.Wallet.Balance;
        }

        internal static void Sync()
        {
            DataRepository.Main.Wallet.Sync();
        }
    }
}