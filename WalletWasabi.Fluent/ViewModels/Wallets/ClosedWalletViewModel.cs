using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		private ObservableCollection<NavBarItemViewModel> _items;

		protected ClosedWalletViewModel(NavigationStateViewModel navigationState, WalletManager walletManager, Wallet wallet) : base(navigationState, wallet)
		{
			_items = new ObservableCollection<NavBarItemViewModel>
			{
				new SettingsPageViewModel(navigationState) { Parent = this }
			};

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

		public ObservableCollection<NavBarItemViewModel> Items
		{
			get => _items;
			set => this.RaiseAndSetIfChanged(ref _items, value);
		}

		public ReactiveCommand<Unit, Unit> OpenWalletCommand { get; }

		public override string IconName => "web_asset_regular";

		public static WalletViewModelBase Create(NavigationStateViewModel navigationState, WalletManager walletManager, Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new ClosedHardwareWalletViewModel(navigationState, walletManager, wallet)
				: wallet.KeyManager.IsWatchOnly
					? new ClosedWatchOnlyWalletViewModel(navigationState, walletManager, wallet)
					: new ClosedWalletViewModel(navigationState, walletManager, wallet);
		}
	}
}
