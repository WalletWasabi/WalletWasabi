using ReactiveUI;
using Splat;
using System;
using System.Reactive.Linq;
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

		private WalletViewModelBase WalletViewModel { get; }

		protected Wallet Wallet => WalletViewModel.Wallet;

		public void ExpandWallet()
		{
			WalletViewModel.IsExpanded = true;
		}
	}
}
