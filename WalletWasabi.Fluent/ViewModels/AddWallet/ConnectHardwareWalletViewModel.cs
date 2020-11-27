using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Hwi;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class HardwareDetectionState
	{
		private HwiClient _client;

		public HardwareDetectionState(string walletName, WalletManager walletManager, Network network)
		{
			WalletName = walletName;
			WalletManager = walletManager;
			Network = network;
			_client = new HwiClient(network);

			Devices = Enumerable.Empty<Hwi.Models.HwiEnumerateEntry>();
		}

		public IEnumerable<Hwi.Models.HwiEnumerateEntry> Devices { get; private set; }

		public Hwi.Models.HwiEnumerateEntry? SelectedDevice { get; set; }

		public WalletManager WalletManager { get; }

		public Network Network { get; }

		public string WalletName { get; }

		public async Task<KeyManager> GenerateWalletAsync()
		{
			var selectedDevice = SelectedDevice;

			if(selectedDevice is null)
			{
				throw new Exception("Cannot be null.");
			}

			var fingerPrint = (HDFingerprint)selectedDevice.Fingerprint;
			using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			var extPubKey = await _client.GetXpubAsync(selectedDevice.Model, selectedDevice.Path, KeyManager.DefaultAccountKeyPath, cts.Token).ConfigureAwait(false);
			var path = WalletManager.WalletDirectories.GetWalletFilePaths(WalletName).walletFilePath;

			return KeyManager.CreateNewHardwareWalletWatchOnly(fingerPrint, extPubKey, path);
		}

		public async Task EnumerateHardwareWalletsAsync(CancellationToken token)
		{
			Devices = (await _client.EnumerateAsync(token).ConfigureAwait(false))
				.Where(wallet => WalletManager.GetWallets()
				.Any(x=>x.KeyManager.MasterFingerprint == wallet.Fingerprint) == false);			
		}
	}

	public class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		public ConnectHardwareWalletViewModel(NavigationStateViewModel navigationState, string walletName, Network network, WalletManager walletManager, bool trySkipPage = true)
			: base(navigationState, NavigationTarget.DialogScreen)
		{
			if (trySkipPage)
			{
				IsBusy = true;

				this.WhenNavigatedTo(() =>
				{
					RxApp.MainThreadScheduler.Schedule(async () =>
					{
						var detectionState = new HardwareDetectionState(walletName, walletManager, network);

						await detectionState.EnumerateHardwareWalletsAsync(CancellationToken.None);

						var deviceCount = detectionState.Devices.Count();

						if (deviceCount == 0)
						{
						// navigate to detecting page.
					}
						else if (deviceCount == 1)
						{
						// navigate to detected hw wallet page.
						detectionState.SelectedDevice = detectionState.Devices.First();

							NavigateTo(new DetectedHardwareWalletViewModel(navigationState, detectionState), NavigationTarget.DialogScreen);
						}
						else
						{
						// Do nothing... stay on this page.
					}

						IsBusy = false;
					});

					return Disposable.Empty;
				});
			}

			NextCommand = ReactiveCommand.Create(() =>
			{
				NavigateTo(new DetectHardwareWalletViewModel(navigationState, walletName, network, walletManager), NavigationTarget.DialogScreen);
			});
		}

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }
	}
}
