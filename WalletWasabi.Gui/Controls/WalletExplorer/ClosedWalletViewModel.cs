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
				IsBusy = true;

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
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
