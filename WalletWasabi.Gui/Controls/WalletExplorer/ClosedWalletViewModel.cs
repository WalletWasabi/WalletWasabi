using System.IO;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	class ClosedWalletViewModel : WalletViewModelBase
	{
		public ClosedWalletViewModel(string walletFile)
		{
			WalletFile = walletFile;

			Title = Path.GetFileNameWithoutExtension(walletFile);
		}

		public string WalletFile { get; }
	}
}
