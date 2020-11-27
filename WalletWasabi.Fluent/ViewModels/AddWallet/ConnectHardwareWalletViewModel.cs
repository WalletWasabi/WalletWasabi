using NBitcoin;
using ReactiveUI;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class ConnectHardwareWalletViewModel : RoutableViewModel
	{
		public ConnectHardwareWalletViewModel(NavigationStateViewModel navigationState, string walletName, Network network, WalletManager walletManager, bool trySkipPage = true)
			: base(navigationState, NavigationTarget.DialogScreen)
		{
			var detectionState = new HardwareDetectionState(walletName, walletManager, network);

			if (trySkipPage)
			{
				IsBusy = true;

				this.WhenNavigatedTo(() =>
				{
					RxApp.MainThreadScheduler.Schedule(async () =>
					{
						await detectionState.EnumerateHardwareWalletsAsync(CancellationToken.None);

						var deviceCount = detectionState.Devices.Count();

						if (deviceCount == 0)
						{
							// navigate to detecting page.
							NavigateTo(new DetectHardwareWalletViewModel(navigationState, detectionState), NavigationTarget.DialogScreen);
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
				NavigateTo(new DetectHardwareWalletViewModel(navigationState, detectionState), NavigationTarget.DialogScreen);
			});
		}

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }
	}
}
