using ReactiveUI;
using System;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.AddWallet.Common
{
	public class EnterPasswordViewModel : ViewModelBase, IRoutableViewModel
	{
		public EnterPasswordViewModel(IScreen screen, Global global)
		{
			var walletGenerator = new WalletGenerator(global.WalletManager.WalletDirectories.WalletsDir, global.Network);
			walletGenerator.TipHeight = global.BitcoinStore.SmartHeaderChain.TipHeight;

			var (km, mnemonic) = walletGenerator.GenerateWallet("TestWallet", "12345");
		}

		public string? UrlPathSegment => throw new NotImplementedException();
		public IScreen HostScreen => throw new NotImplementedException();
	}
}
