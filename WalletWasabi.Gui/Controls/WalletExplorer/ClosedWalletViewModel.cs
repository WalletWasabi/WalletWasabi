using ReactiveUI;
using System.Reactive;
using System;
using System.Linq;
using AvalonStudio.Extensibility;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		public ClosedWalletViewModel(string path) : base (new Wallet(path))
		{
			OpenWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				IsBusy = true;
				await Wallet.LoadWalletAsync();

				IsBusy = false;
				IoC.Get<WalletExplorerViewModel>().RemoveWallet(this);

				// Open Wallet Explorer tabs
				if (Wallet.WalletService.Coins.Any())
				{
					// If already have coins then open the last active tab first.
					IoC.Get<WalletExplorerViewModel>().OpenWallet(Wallet, receiveDominant: false);
				}
				else // Else open with Receive tab first.
				{
					IoC.Get<WalletExplorerViewModel>().OpenWallet(Wallet, receiveDominant: true);
				}
			});
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
