using AvalonStudio.Extensibility;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
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

					await global.WalletManager.StartWalletAsync(Wallet);
				}
				catch (TaskCanceledException ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
				{
					Logger.LogTrace(ex);
				}
				catch (Exception ex)
				{
					NotificationHelpers.Error($"Couldn't load wallet: {Title}. Reason: {ex.ToUserFriendlyString()}");
					Logger.LogError(ex);
				}
			}, this.WhenAnyValue(x => x.WalletState).Select(x => x == WalletState.Uninitialized));
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
