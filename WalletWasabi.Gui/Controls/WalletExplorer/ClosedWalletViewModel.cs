using AvalonStudio.Extensibility;
using ReactiveUI;
using Splat;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		public ClosedWalletViewModel(string path) : base(Path.GetFileNameWithoutExtension(path))
		{
			OpenWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					IsBusy = true;

					var global = Locator.Current.GetService<Global>();

					if (!await global.WaitForInitializationCompletedAsync(CancellationToken.None))
					{
						return;
					}

					var walletManager = global.WalletManager;

					var keyManager = global.LoadKeyManager(Title);

					if (keyManager is null)
					{
						return;
					}

					var wallet = await walletManager.CreateAndStartWalletAsync(keyManager);

					var walletExplorer = IoC.Get<WalletExplorerViewModel>();

					var select = walletExplorer.SelectedItem == this;

					walletExplorer.RemoveWallet(this);

					if (wallet.Coins.Any())
					{
						// If already have coins then open the last active tab first.
						walletExplorer.OpenWallet(wallet, receiveDominant: false, select: select);
					}
					else // Else open with Receive tab first.
					{
						walletExplorer.OpenWallet(wallet, receiveDominant: true, select: select);
					}
				}
				catch (Exception e)
				{
					NotificationHelpers.Error($"Error loading Wallet: {Title}");
					Logger.LogError(e.Message);
				}
				finally
				{
					IsBusy = false;
				}
			}, this.WhenAnyValue(x => x.IsBusy).Select(x => !x));
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
