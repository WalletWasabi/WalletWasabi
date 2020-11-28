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
		private readonly bool _trySkipPage;
		private readonly HardwareDetectionState _detectionState;

		public ConnectHardwareWalletViewModel(NavigationStateViewModel navigationState, string walletName, Network network, WalletManager walletManager, bool trySkipPage = true)
			: base(navigationState)
		{
			_detectionState = new HardwareDetectionState(walletName, walletManager, network);

			_trySkipPage = trySkipPage;

			if (trySkipPage)
			{
				IsBusy = true;
			}

			NextCommand = ReactiveCommand.Create(() =>
			{
				NavigateTo(new DetectHardwareWalletViewModel(navigationState, _detectionState), NavigationTarget.DialogScreen);
			});
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			if (!inStack && _trySkipPage)
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					await _detectionState.EnumerateHardwareWalletsAsync(CancellationToken.None);

					var deviceCount = _detectionState.Devices.Count();

					if (deviceCount == 0)
					{
						// navigate to detecting page.
						NavigateTo(new DetectHardwareWalletViewModel(NavigationState, _detectionState), NavigationTarget.DialogScreen);
					}
					else if (deviceCount == 1)
					{
						// navigate to detected hw wallet page.
						_detectionState.SelectedDevice = _detectionState.Devices.First();

						NavigateTo(new DetectedHardwareWalletViewModel(NavigationState, _detectionState), NavigationTarget.DialogScreen);
					}
					else
					{
						// Do nothing... stay on this page.
					}

					IsBusy = false;
				});
			}
		}

		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }
	}
}
