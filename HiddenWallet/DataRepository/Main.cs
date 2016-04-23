// Contains base data classes and database communication.  Sometimes 
// also hold a directory that contains any SQL procs or other specific code.

using HiddenWallet.DataClasses;
using NBitcoin;

namespace HiddenWallet.DataRepository
{
    internal static class Main
    {
        internal const string PathMainWalletFile = @"Wallets\DefaultMainWallet.hid";
        internal const string PathTestWalletFile = @"Wallets\DefaultTestWallet.hid";
        internal static Network Network;
        internal static DataClasses.Main.WalletFileStructure WalletFileContent;
        internal static Wallet Wallet;
    }
}