using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		protected ClosedWalletViewModel(WalletManager walletManager, Wallet wallet) : base(wallet)
		{
			OpenWalletCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					try
					{
						if (wallet.KeyManager.PasswordVerified is true)
						{
							// TODO ... new UX will test password earlier...
						}

						await Task.Run(async () => await walletManager.StartWalletAsync(Wallet));
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

		public static WalletViewModelBase Create(WalletManager walletManager, Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new ClosedHardwareWalletViewModel(walletManager, wallet)
				: wallet.KeyManager.IsWatchOnly
					? new ClosedWatchOnlyWalletViewModel(walletManager, wallet)
					: new ClosedWalletViewModel(walletManager, wallet);
		}
	}
}
