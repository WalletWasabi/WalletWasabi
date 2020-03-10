using AvalonStudio.Extensibility;
using ReactiveUI;
using Splat;
using System.IO;
using System.Linq;
using System.Reactive;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		public ClosedWalletViewModel(string path) : base (Path.GetFileNameWithoutExtension(path))
		{
			OpenWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				IsBusy = true;

				var global = Locator.Current.GetService<Global>();

				if (!await global.WaitForInitializationCompletedAsync())
				{
					return;
				}

				var walletManager = global.WalletManager;

				var walletFullPath = global.GetWalletFullPath(Title);
				var walletBackupFullPath = global.GetWalletBackupFullPath(Title);

				var keyManager = global.LoadKeyManager(walletFullPath, walletBackupFullPath);

				if (keyManager is null)
				{
					IsBusy = false;
					return;
				}

				var walletService = await walletManager.CreateAndStartWalletServiceAsync(keyManager);

				IoC.Get<WalletExplorerViewModel>().RemoveWallet(this);

				if (walletService.Coins.Any())
				{
					// If already have coins then open the last active tab first.
					IoC.Get<WalletExplorerViewModel>().OpenWallet(walletService, receiveDominant: false);
				}
				else // Else open with Receive tab first.
				{
					IoC.Get<WalletExplorerViewModel>().OpenWallet(walletService, receiveDominant: true);
				}

				IsBusy = false;
			});
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
