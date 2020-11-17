using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;
using HardwareWalletViewModel = WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets.HardwareWalletViewModel;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		private readonly string _walletName;
		private readonly Network _network;
		private readonly List<Wallet> _currentWallets;
		private CancellationTokenSource _searchHardwareWalletCts;
		private HardwareWalletViewModel? _selectedHardwareWallet;

		public ConnectHardwareWalletViewModel(NavigationStateViewModel navigationState, string walletName, Network network,
			WalletManager walletManager) : base(navigationState, NavigationTarget.DialogScreen)
		{
			_walletName = walletName;
			_network = network;
			_currentWallets = walletManager.GetWallets().ToList();
			_searchHardwareWalletCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			HardwareWallets = new ObservableCollection<HardwareWalletViewModel>();

			Task.Run(StartHardwareWalletDetection);

			CancelCommand = ReactiveCommand.Create(ClearNavigation);
			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			var connectCommandIsExecute = this.WhenAnyValue(x => x.SelectedHardwareWallet).Select(selectedHardwareWallet => selectedHardwareWallet?.HardwareWalletInfo.Fingerprint is { });
			NextCommand = ReactiveCommand.Create(() => { },connectCommandIsExecute);
		}

		public HardwareWalletViewModel? SelectedHardwareWallet
		{
			get => _selectedHardwareWallet;
			set => this.RaiseAndSetIfChanged(ref _selectedHardwareWallet, value);
		}

		public ObservableCollection<HardwareWalletViewModel> HardwareWallets { get; }

		public ICommand NextCommand { get; }

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }

		public string UDevRulesLink => "https://github.com/bitcoin-core/HWI/tree/master/hwilib/udev";

		protected async Task StartHardwareWalletDetection()
		{
			while (!_searchHardwareWalletCts.IsCancellationRequested)
			{
				try
				{
					// Reset token
					_searchHardwareWalletCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

					var client = new HwiClient(_network);
					var detectedHardwareWallets = (await client.EnumerateAsync(_searchHardwareWalletCts.Token)).Select(x => new HardwareWalletViewModel(x)).ToList();

					// Remove wallets that are already added to software
					var walletsToRemove = detectedHardwareWallets.Where(wallet => _currentWallets.Any(x => x.KeyManager.MasterFingerprint == wallet.HardwareWalletInfo.Fingerprint));
					detectedHardwareWallets.RemoveMany(walletsToRemove);

					// Remove disconnected hardware wallets from the list
					HardwareWallets.RemoveMany(HardwareWallets.Except(detectedHardwareWallets));

					// Remove detected wallets that are already in the list.
					detectedHardwareWallets.RemoveMany(HardwareWallets);

					// All remained detected hardware wallet is new so add.
					HardwareWallets.AddRange(detectedHardwareWallets);
				}
				catch (Exception ex)
				{
					// TODO: log
				}
			}
		}
	}
}