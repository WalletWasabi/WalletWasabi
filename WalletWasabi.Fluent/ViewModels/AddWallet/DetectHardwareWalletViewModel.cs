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
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using HardwareWalletViewModel = WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets.HardwareWalletViewModel;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class DetectHardwareWalletViewModel : RoutableViewModel
	{
		private HardwareWalletViewModel? _selectedHardwareWallet;
		private HardwareDetectionState _detectionState;

		public DetectHardwareWalletViewModel(NavigationStateViewModel navigationState, HardwareDetectionState detectionState)
			: base(navigationState)
		{
			HardwareWallets = new ObservableCollection<HardwareWalletViewModel>();
			_detectionState = detectionState;


			OpenBrowserCommand = ReactiveCommand.CreateFromTask<string>(IoHelpers.OpenBrowserAsync);

				this.WhenAnyValue(x => x.SelectedHardwareWallet)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Select(x => x?.HardwareWalletInfo.Fingerprint is { } && x.HardwareWalletInfo.IsInitialized())
					.Subscribe(async x=>
					{
						await ConnectSelectedHardwareWalletAsync();
					});

			this.WhenAnyValue(x => x.SelectedHardwareWallet)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Where(x => x is { } && !x.HardwareWalletInfo.IsInitialized() && x.HardwareWalletInfo.Model != HardwareWalletModels.Coldcard)
				.Subscribe(async x =>
				{
					if (DisposeCts is null)
					{
						return;
					}

					using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(21));
					using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, DisposeCts.Token);

					// Trezor T doesn't require interactive mode.
					var interactiveMode = !(x!.HardwareWalletInfo.Model == HardwareWalletModels.Trezor_T || x.HardwareWalletInfo.Model == HardwareWalletModels.Trezor_T_Simulator);

					try
					{
						// TODO: Notify the user to check the device

						await _detectionState.Client.SetupAsync(x.HardwareWalletInfo.Model, x.HardwareWalletInfo.Path, interactiveMode, initCts.Token);

						// todo... check this doesnt trigger the other navigage to.
						await ConnectSelectedHardwareWalletAsync();
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				});
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			DisposeCts = new CancellationTokenSource();

			StartDetection();

			disposable.Add(Disposable.Create(() =>
			{
				DisposeCts.Cancel();
				DisposeCts.Dispose();
			}));
		}

		private Task? DetectionTask { get; set; }

		private CancellationTokenSource? DetectionCts { get; set; }

		private CancellationTokenSource? DisposeCts { get; set; }

		public HardwareWalletViewModel? SelectedHardwareWallet
		{
			get => _selectedHardwareWallet;
			set => this.RaiseAndSetIfChanged(ref _selectedHardwareWallet, value);
		}

		public ObservableCollection<HardwareWalletViewModel> HardwareWallets { get; }

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }

		private async Task ConnectSelectedHardwareWalletAsync()
		{
			if (SelectedHardwareWallet?.HardwareWalletInfo.Fingerprint is null)
			{
				return;
			}

			try
			{
				IsBusy = true;
				await StopDetectionAsync();

				_detectionState.SelectedDevice = SelectedHardwareWallet.HardwareWalletInfo;

				NavigateTo(new DetectedHardwareWalletViewModel(NavigationState, _detectionState), NavigationTarget.DialogScreen);
			}
			catch (Exception ex)
			{
				// TODO: Notify the user about the error
				Logger.LogError(ex);

				// Restart detection
				StartDetection();
			}
			finally
			{
				IsBusy = false;
			}
		}

		private async Task StopDetectionAsync()
		{
			if (DetectionTask is { } task && DetectionCts is { } cts)
			{
				cts.Cancel();
				await task;
			}
		}

		private void StartDetection()
		{
			if (DisposeCts is { } cts)
			{
				DetectionCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
				DetectionTask = HardwareWalletDetectionAsync(DetectionCts);
			}
		}

		protected async Task HardwareWalletDetectionAsync(CancellationTokenSource detectionCts)
		{
			while (!detectionCts.IsCancellationRequested)
			{
				var sw = Stopwatch.StartNew();

				try
				{
					using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

					var detectedHardwareWallets = (await _detectionState.Client.EnumerateAsync(timeoutCts.Token).ConfigureAwait(false)).Select(x => new HardwareWalletViewModel(x)).ToArray();
					detectionCts.Token.ThrowIfCancellationRequested();

					// The wallets that already exist in the software.
					var alreadyExistingWalletsToRemove = detectedHardwareWallets.Where(wallet => _detectionState.WalletManager.GetWallets().Any(x => x.KeyManager.MasterFingerprint == wallet.HardwareWalletInfo.Fingerprint));
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

						if (SelectedHardwareWallet is null)
						{
							SelectedHardwareWallet = HardwareWallets.FirstOrDefault();
						}
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
					await Task.Delay(5000 - sw.Elapsed.Milliseconds).ConfigureAwait(false);
				}
			}
		}
	}
}
