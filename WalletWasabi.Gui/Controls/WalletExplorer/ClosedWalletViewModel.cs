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
		protected ClosedWalletViewModel(Wallet wallet) : base(wallet)
		{
			OpenWalletCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					var global = Locator.Current.GetService<Global>();

					await Task.Run(async () => await global.WalletManager.StartWalletAsync(Wallet));
				}
				catch (OperationCanceledException ex)
				{
					Logger.LogTrace(ex);
				}
				catch (Exception ex)
				{
					NotificationHelpers.Error($"Couldn't load wallet. Reason: {ex.ToUserFriendlyString()}", sender: wallet);
					Logger.LogError(ex);
				}
			}, this.WhenAnyValue(x => x.WalletState).Select(x => x == WalletState.Uninitialized));
		}

		public static WalletViewModelBase Create(Wallet wallet)
		{
			if (wallet.KeyManager.IsHardwareWallet)
			{
				return new ClosedHardwareWalletViewModel(wallet);
			}
			else if (wallet.KeyManager.IsWatchOnly)
			{
				return new ClosedWatchOnlyWalletViewModel(wallet);
			}
			else
			{
				return new ClosedWalletViewModel(wallet);
			}
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }
	}
}
