// Performs all CRUD (Create, Read, Sync, Delete) actions with the Data, done in a way that the
// repository can be changed out with no need to rewrite any higher level code.

using System.Windows.Forms;
using NBitcoin;

namespace HiddenWallet.Services
{
    internal static class Main
    {
        internal static void LoadSettings(bool restart)
        {
            var network = "MainNet";

            if (WalletServices.WalletExists())
            {
                DataRepository.Main.WalletFileContent = new DataClasses.Main.WalletFileStructure(WalletServices.GetPathWalletFile());
                network = DataRepository.Main.WalletFileContent.Network;
            }
            switch (network)
            {
                case "TestNet":
                    DataRepository.Main.Network = Network.TestNet;
                    break;
                default:
                    DataRepository.Main.Network = Network.Main;
                    break;
            }
            if (restart)
                Application.Restart();
        }
    }
}