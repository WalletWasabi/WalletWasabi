using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		protected ClosedWalletViewModel(Wallet wallet) : base(wallet)
		{
			OpenWalletCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					try
					{
						var global = Locator.Current.GetService<Global>();

						if (wallet.KeyManager.PasswordVerified is true)
						{
							await Task.Run(async () => await global.WalletManager.StartWalletAsync(Wallet));
						}
						else
						{
							IoC.Get<IShell>().GetOrCreateByType<WalletManagerViewModel>().SelectTestPassword(wallet.WalletName);
						}
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
				},
				this.WhenAnyValue(x => x.WalletState).Select(x => x == WalletState.Uninitialized));
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }

		public static WalletViewModelBase Create(Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new ClosedHardwareWalletViewModel(wallet)
				: wallet.KeyManager.IsWatchOnly
					? new ClosedWatchOnlyWalletViewModel(wallet)
					: new ClosedWalletViewModel(wallet);
		}
	}
}
