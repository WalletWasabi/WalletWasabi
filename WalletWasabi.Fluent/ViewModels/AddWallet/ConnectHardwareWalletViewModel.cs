using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		private readonly Network _network;
		private readonly List<Wallet> _currentWallets;
		private CancellationTokenSource _searchHardwareWalletCts;
		private HardwareWalletViewModel _selectedHardwareWallet;

		public ConnectHardwareWalletViewModel(NavigationStateViewModel navigationState, Network network,
			WalletManager walletManager) : base(navigationState, NavigationTarget.DialogScreen)
		{
			_network = network;
			_currentWallets = walletManager.GetWallets().ToList();
			_searchHardwareWalletCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			HardwareWallets = new ObservableCollection<HardwareWalletViewModel>();

			EnumerateIfHardwareWalletsAsync();

			CancelCommand = ReactiveCommand.Create(() => navigationState.DialogScreen?.Invoke().Router.NavigationStack.Clear());

			var connectCommandIsExecute = this.WhenAnyValue(x => x.SelectedHardwareWallet).Select(selectedHardwareWallet => selectedHardwareWallet is { });
			NextCommand = ReactiveCommand.Create(() => { },connectCommandIsExecute);
		}

		public HardwareWalletViewModel SelectedHardwareWallet
		{
			get => _selectedHardwareWallet;
			set => this.RaiseAndSetIfChanged(ref _selectedHardwareWallet, value);
		}

		public ObservableCollection<HardwareWalletViewModel> HardwareWallets { get; }

		public ICommand NextCommand { get; }

		protected async Task EnumerateIfHardwareWalletsAsync()
		{
			while (!_searchHardwareWalletCts.IsCancellationRequested)
			{
				try
				{
					_searchHardwareWalletCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

					var client = new HwiClient(_network);
					var detectedHardwareWallets = (await client.EnumerateAsync(_searchHardwareWalletCts.Token)).Select(x => new HardwareWalletViewModel(x)).ToList();

					// Remove wallets that are already added to software
					foreach (var wallet in detectedHardwareWallets.ToList().Where(wallet => _currentWallets.Any(x => x.KeyManager.MasterFingerprint == wallet.HardwareWalletInfo.Fingerprint)))
					{
						detectedHardwareWallets.Remove(wallet);
					}

					// Remove disconnected hardware wallets
					foreach (var wallet in HardwareWallets.ToList().Where(wallet => !detectedHardwareWallets.Any(x => x.Equals(wallet))))
					{
						HardwareWallets.Remove(wallet);
					}

					// Remove wallets that are already detected
					foreach (var wallet in HardwareWallets)
					{
						var walletToDelete = detectedHardwareWallets.FirstOrDefault(x => x.Equals(wallet));

						if (walletToDelete is { })
						{
							detectedHardwareWallets.Remove(walletToDelete);
						}
					}

					// Add newly detected hardware wallets
					HardwareWallets.AddRange(detectedHardwareWallets);
				}
				catch (Exception ex)
				{

				}
			}
		}
	}
}