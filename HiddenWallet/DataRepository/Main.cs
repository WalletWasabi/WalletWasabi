// Contains base data classes and database communication.  Sometimes 
// also hold a directory that contains any SQL procs or other specific code.

using HiddenWallet.DataClasses;
using NBitcoin;

namespace HiddenWallet.DataRepository
{
    internal static class Main
    {
        internal static Network Network;
        internal static string PathWalletFile = @"Wallet\Wallet.hid";
        internal static Wallet Wallet;
    }
}