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
		private HardwareWalletViewModel? _selectedHardwareWallet;
		private bool _walletListVisible;

		public ConnectHardwareWalletViewModel(NavigationStateViewModel navigationState, string walletName, Network network, WalletManager walletManager)
			: base(navigationState, NavigationTarget.DialogScreen)
		{
			WalletName = walletName;
			WalletManager = walletManager;
			HwiClient = new HwiClient(network);
			HardwareWallets = new ObservableCollection<HardwareWalletViewModel>();

			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

			var nextCommandIsExecute =
				this.WhenAnyValue(x => x.SelectedHardwareWallet)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Select(x => x?.HardwareWalletInfo.Fingerprint is { } && x.HardwareWalletInfo.IsInitialized());
			NextCommand = ReactiveCommand.Create(ConnectSelectedHardwareWallet,nextCommandIsExecute);

			this.WhenAnyValue(x => x.SelectedHardwareWallet)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Where(x => x is { } && !x.HardwareWalletInfo.IsInitialized() && x.HardwareWalletInfo.Model != HardwareWalletModels.Coldcard)
				.Subscribe(async x =>
				{
					if (NavigatedToCts is null)
					{
						return;
					}

					using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(21));
					using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, NavigatedToCts.Token);

					// Trezor T doesn't require interactive mode.
					var interactiveMode = !(x!.HardwareWalletInfo.Model == HardwareWalletModels.Trezor_T || x.HardwareWalletInfo.Model == HardwareWalletModels.Trezor_T_Simulator);

					try
					{
						// TODO: Notify the user to check the device
						await HwiClient.SetupAsync(x.HardwareWalletInfo.Model, x.HardwareWalletInfo.Path, interactiveMode, initCts.Token);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				});

			this.WhenNavigatedTo(() =>
			{
				NavigatedToCts = new CancellationTokenSource();

				StartDetection();

				return Disposable.Create(() =>
				{
					NavigatedToCts.Cancel();
					NavigatedToCts.Dispose();
				});
			});

			this.WhenAnyValue(x => x.HardwareWallets.Count)
				.Subscribe(x =>
				{
					WalletListVisible = x > 0;
				});
		}

		private string WalletName  { get; }

		private WalletManager WalletManager  { get; }

		private HwiClient HwiClient  { get; }

		private Task? DetectionTask  { get; set; }

		private CancellationTokenSource? DetectionCts  { get; set; }

		private CancellationTokenSource? NavigatedToCts { get; set; }

		public HardwareWalletViewModel? SelectedHardwareWallet
		{
			get => _selectedHardwareWallet;
			set => this.RaiseAndSetIfChanged(ref _selectedHardwareWallet, value);
		}		

		public bool WalletListVisible
		{
			get => _walletListVisible;
			set => this.RaiseAndSetIfChanged(ref _walletListVisible, value);
		}

		public ObservableCollection<HardwareWalletViewModel> HardwareWallets { get; }

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
				var extPubKey = await HwiClient.GetXpubAsync(SelectedHardwareWallet.HardwareWalletInfo.Model, SelectedHardwareWallet.HardwareWalletInfo.Path, KeyManager.DefaultAccountKeyPath, cts.Token);
				var path = WalletManager.WalletDirectories.GetWalletFilePaths(WalletName).walletFilePath;

				WalletManager.AddWallet(KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, extPubKey, path));

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
			if (DetectionTask is { } task && DetectionCts is { } cts)
			{
				cts.Cancel();
				await task;
			}
		}

		private void StartDetection()
		{
			if (NavigatedToCts is { } cts)
			{
				DetectionCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
				DetectionTask = HardwareWalletDetectionAsync(DetectionCts);
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

					var detectedHardwareWallets = (await HwiClient.EnumerateAsync(timeoutCts.Token)).Select(x => new HardwareWalletViewModel(x)).ToArray();
					detectionCts.Token.ThrowIfCancellationRequested();

					// The wallets that are already exists in the wallets.
					var alreadyExistingWalletsToRemove = detectedHardwareWallets.Where(wallet => WalletManager.GetWallets().Any(x => x.KeyManager.MasterFingerprint == wallet.HardwareWalletInfo.Fingerprint));
					// The wallets that are not detectable since the last enumeration.
					var disconnectedWalletsToRemove = HardwareWallets.Except(detectedHardwareWallets);

					var toRemove = alreadyExistingWalletsToRemove.Union(disconnectedWalletsToRemove).ToArray();
					var toAdd = detectedHardwareWallets.Except(toRemove).Except(HardwareWallets);

					await Observable.Start(() =>
					{
						// Remove disconnected hardware wallets from the list
						HardwareWallets.RemoveMany(toRemove);
						// Add newly detected hardware wallets
						HardwareWallets.AddRange(toAdd);
					}, RxApp.MainThreadScheduler);

				}
				catch (Exception ex)
				{
					if (ex is not OperationCanceledException)
					{
						Logger.LogError(ex);
					}
				}

				// Too fast enumeration causes the detected hardware wallets to be unable to provide the fingerprint.
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