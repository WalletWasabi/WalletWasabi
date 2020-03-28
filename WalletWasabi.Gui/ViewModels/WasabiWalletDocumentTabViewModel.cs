using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class WasabiWalletDocumentTabViewModel : WasabiDocumentTabViewModel
	{
		protected WasabiWalletDocumentTabViewModel(string title, WalletViewModelBase walletViewModel)
			: base(title)
		{
			WalletViewModel = walletViewModel;
		}

		public WalletViewModelBase WalletViewModel { get; }
		public Wallet Wallet => WalletViewModel.Wallet;
	}
}
