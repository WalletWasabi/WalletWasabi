using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;

[NavigationMetaData(Title = "Hardware Wallet")]
public partial class DetectedHardwareWalletViewModel : RoutableViewModel
{
	[AutoNotify] private bool _enableCoinjoin;
	[AutoNotify] private bool _isBridgeUnavailable;

	public DetectedHardwareWalletViewModel(UiContext uiContext, WalletCreationOptions.ConnectToHardwareWallet options) : base(uiContext)
	{
		var (walletName, device, _) = options;

		ArgumentException.ThrowIfNullOrEmpty(walletName);
		ArgumentNullException.ThrowIfNull(device);

		WalletName = walletName;

		Type = device.WalletType;

		TypeName = device.Model.FriendlyName();

		// Coinjoin is opt-in: only offer it for models that can sign coinjoins on the device.
		SupportsCoinjoin = device.SupportsCoinJoin();

		SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

		EnableBack = false;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(options with { EnableCoinjoin = EnableCoinjoin }));

		NoCommand = ReactiveCommand.Create(OnNo);

		EnableAutoBusyOn(NextCommand);
	}

	public CancellationTokenSource? CancelCts { get; private set; }

	public string WalletName { get; }

	public WalletType Type { get; }

	public string TypeName { get; }

	public bool SupportsCoinjoin { get; }

	public ICommand NoCommand { get; }

	private async Task OnNextAsync(WalletCreationOptions.ConnectToHardwareWallet options)
	{
		try
		{
			CancelCts ??= new CancellationTokenSource();
			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options, CancelCts.Token);
			Navigate().To().AddedWalletPage(walletSettings, options);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "Error occurred during adding your wallet.");
			Navigate().Back();
		}
	}

	private void OnNo()
	{
		Navigate().Back();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);

		disposables.Add(Disposable.Create(() =>
		{
			CancelCts?.Cancel();
			CancelCts?.Dispose();
			CancelCts = null;
		}));

		// Warn up front when coinjoin is offered but the bridge that it needs is not running, so the user
		// can start Trezor Suite before checking the box instead of hitting an error after confirming.
		if (SupportsCoinjoin)
		{
			Task.Run(async () =>
			{
				try
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
					IsBridgeUnavailable = !await WalletWasabi.Hwi.Trezor.TrezorDevice.IsBridgeAvailableAsync(cts.Token);
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
		}
	}
}
