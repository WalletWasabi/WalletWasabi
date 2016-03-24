// Performs all CRUD (Create, Read, Update, Delete) actions with the Data, done in a way that the
// repository can be changed out with no need to rewrite any higher level code.

using HiddenWallet.DataClasses;

namespace HiddenWallet.Services
{
    internal static class Main
    {
        internal static void CreateWallet(string password)
        {
            DataRepository.Main.Wallet = new Wallet(
                DataRepository.Main.WalletPath,
                DataRepository.Main.Network,
                password);
        }
    }
}