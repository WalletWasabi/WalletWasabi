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
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		public ClosedWalletViewModel(Wallet wallet) : base(wallet)
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

					await global.WalletManager.StartWalletAsync(wallet.KeyManager);

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
