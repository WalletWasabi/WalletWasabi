// Performs all CRUD (Create, Read, Sync, Delete) actions with the Data, done in a way that the
// repository can be changed out with no need to rewrite any higher level code.

using System.Windows.Forms;
using HiddenWallet.Properties;
using NBitcoin;

namespace HiddenWallet.Services
{
    internal static class Main
    {
        internal static void LoadSettings(bool restart)
        {
            switch (Settings.Default.Network)
            {
                case "Test":
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