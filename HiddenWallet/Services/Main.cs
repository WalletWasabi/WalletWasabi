// Performs all CRUD (Create, Read, Update, Delete) actions with the Data, done in a way that the
// repository can be changed out with no need to rewrite any higher level code.

using System.IO;
using HiddenWallet.DataClasses;
using HiddenWallet.Properties;

namespace HiddenWallet.Services
{
    internal static class Main
    {
        internal static void CreateWallet(string password)
        {
            DataRepository.Main.Wallet = new Wallet(
                DataRepository.Main.PathWalletFile,
                DataRepository.Main.Network,
                password);
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
    }
}