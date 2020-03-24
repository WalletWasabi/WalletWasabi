using AvalonStudio.Extensibility;
using ReactiveUI;
using Splat;
using System;
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
					var global = Locator.Current.GetService<Global>();

					if (!await global.WaitForInitializationCompletedAsync(CancellationToken.None))
					{
						return;
					}

					await global.WalletManager.StartWalletAsync(Wallet);
				}
				catch (Exception e)
				{
					NotificationHelpers.Error($"Error loading Wallet: {Title}");
					Logger.LogError(e.Message);
				}
			}, this.WhenAnyValue(x => x.IsBusy).Select(x => !x));

			this.WhenAnyValue(x => x.WalletState)
				.Where(x => x == WalletState.Started)
				.Take(1)
				.Subscribe(x =>
				{
					var walletExplorer = IoC.Get<WalletExplorerViewModel>();

					var select = walletExplorer.SelectedItem == this;

					walletExplorer.RemoveWallet(this);

					walletExplorer.OpenClosedWallet(this);
				});
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
