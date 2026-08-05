using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Authorize Coinjoin on Your Device", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CoinJoinAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly WalletCoinjoinModel _walletCoinjoinModel;

	// Shown when nothing can reach the device at all: the fix is installing that, not checking the device.
	[AutoNotify] private bool _isTransportMissing;

	public CoinJoinAuthDialogViewModel(UiContext uiContext, WalletCoinjoinModel walletCoinjoinModel, WalletType walletType, int maxRounds, decimal maxMiningFeeRate) : base(uiContext)
	{
		_walletCoinjoinModel = walletCoinjoinModel;
		WalletType = walletType;
		LimitsText = $"The device will ask to approve at most {maxRounds} rounds at up to {maxMiningFeeRate:0.##} sat/vByte mining fee rate.";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = $"Authorization failed.{Environment.NewLine}Please, check your device and try again.";

		OpenBridgeDownloadCommand = ReactiveCommand.CreateFromTask(() => UiContext.FileSystem.OpenBrowserAsync(HardwareWalletService.BridgeDownloadUrl));
	}

	public WalletType WalletType { get; }

	public string LimitsText { get; }

	public ICommand OpenBridgeDownloadCommand { get; }

	protected override async Task<bool> AuthorizeAsync()
	{
		var authorized = await _walletCoinjoinModel.AuthorizeOnDeviceAsync().ConfigureAwait(true);
		IsTransportMissing = !authorized && _walletCoinjoinModel.DeviceAuthorization == DeviceAuthorizationStatus.TransportNotFound;
		if (!authorized)
		{
			// The backend knows which device it was talking to and what it needs, so show what it said.
			AuthorizationFailedMessage = _walletCoinjoinModel.DeviceAuthorizationError is { Length: > 0 } reason
				? reason
				: $"Authorization failed.{Environment.NewLine}Please, check your device and try again.";
		}

		return authorized;
	}
}
