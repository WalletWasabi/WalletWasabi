using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using HardwareWalletViewModel = WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets.HardwareWalletViewModel;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		private readonly string _walletName;
		private readonly WalletManager _walletManager;
		private readonly HwiClient _hwiClient;
		private Task? _detectionTask;
		private CancellationTokenSource? _detectionCts;
		private CancellationTokenSource? _navigatedToCts;
		private HardwareWalletViewModel? _selectedHardwareWallet;

		public ConnectHardwareWalletViewModel(NavigationStateViewModel navigationState, string walletName, Network network, WalletManager walletManager)
			: base(navigationState, NavigationTarget.DialogScreen)
		{
			_walletName = walletName;
			_walletManager = walletManager;
			_hwiClient = new HwiClient(network);
			HardwareWallets = new ObservableCollection<HardwareWalletViewModel>();

			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			var nextCommandIsExecute =
				this.WhenAnyValue(x => x.SelectedHardwareWallet)
					.Select(x => x?.HardwareWalletInfo.Fingerprint is { } && x.HardwareWalletInfo.IsInitialized());
			NextCommand = ReactiveCommand.Create(ConnectSelectedHardwareWallet,nextCommandIsExecute);

			this.WhenAnyValue(x => x.SelectedHardwareWallet)
				.Where(x => x is { } && !x.HardwareWalletInfo.IsInitialized() && x.HardwareWalletInfo.Model != HardwareWalletModels.Coldcard)
				.Subscribe(async x =>
				{
					// TODO: Notify the user to check the device
					using var ctsSetup = new CancellationTokenSource(TimeSpan.FromMinutes(21));

					// Trezor T doesn't require interactive mode.
					var interactiveMode = !(x!.HardwareWalletInfo.Model == HardwareWalletModels.Trezor_T || x.HardwareWalletInfo.Model == HardwareWalletModels.Trezor_T_Simulator);

					try
					{
						await _hwiClient.SetupAsync(x.HardwareWalletInfo.Model, x.HardwareWalletInfo.Path, interactiveMode, ctsSetup.Token);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				});

			this.WhenNavigatedTo(() =>
			{
				_navigatedToCts = new CancellationTokenSource();

				StartDetection();

				return Disposable.Create(() =>
				{
					_navigatedToCts.Cancel();
					_navigatedToCts.Dispose();
				});
			});
		}

		public HardwareWalletViewModel? SelectedHardwareWallet
		{
			get => _selectedHardwareWallet;
			set => this.RaiseAndSetIfChanged(ref _selectedHardwareWallet, value);
		}

		public ObservableCollection<HardwareWalletViewModel> HardwareWallets { get; }

		public ICommand NextCommand { get; }

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }

		private async void ConnectSelectedHardwareWallet()
		{
			if (SelectedHardwareWallet?.HardwareWalletInfo.Fingerprint is null)
			{
				return;
			}

			try
			{
				// TODO: Progress ring
				await StopDetection();

				var fingerPrint = (HDFingerprint)SelectedHardwareWallet.HardwareWalletInfo.Fingerprint;

				using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
				var extPubKey = await _hwiClient.GetXpubAsync(SelectedHardwareWallet.HardwareWalletInfo.Model, SelectedHardwareWallet.HardwareWalletInfo.Path, KeyManager.DefaultAccountKeyPath, cts.Token);

				var path = _walletManager.WalletDirectories.GetWalletFilePaths(_walletName).walletFilePath;

				_walletManager.AddWallet(KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, extPubKey, path));

				// Close dialog
				ClearNavigation();
			}
			catch (Exception ex)
			{
				// TODO: Notify the user about the error
				Logger.LogError(ex);

				// Restart detection
				StartDetection();
			}
		}

		private async Task StopDetection()
		{
			if (_detectionTask is { } task && _detectionCts is { } cts)
			{
				cts.Cancel();
				await task;
			}
		}

		private void StartDetection()
		{
			if (_navigatedToCts is { } cts)
			{
				_detectionCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
				_detectionTask = HardwareWalletDetectionAsync(_detectionCts);
			}
		}

		private async Task HardwareWalletDetectionAsync(CancellationTokenSource detectionCts)
		{
			while (!detectionCts.IsCancellationRequested)
			{
				var sw = Stopwatch.StartNew();

				try
				{
					using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

					var detectedHardwareWallets = (await _hwiClient.EnumerateAsync(timeoutCts.Token)).Select(x => new HardwareWalletViewModel(x)).ToList();
					detectionCts.Token.ThrowIfCancellationRequested();

					// Remove wallets that are already added to software
					var walletsToRemove = detectedHardwareWallets.Where(wallet => _walletManager.GetWallets().Any(x => x.KeyManager.MasterFingerprint == wallet.HardwareWalletInfo.Fingerprint));
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
					if (ex is not OperationCanceledException)
					{
						Logger.LogError(ex);
					}
				}

				// Too fast enumeration causes the detected hardware wallets cannot provide the fingerprint.
				// Wait at least 5 seconds between two enumerations.
				sw.Stop();
				if (sw.Elapsed.Milliseconds < 5000)
				{
					await Task.Delay(5000 - sw.Elapsed.Milliseconds);
				}
			}
		}
	}
}