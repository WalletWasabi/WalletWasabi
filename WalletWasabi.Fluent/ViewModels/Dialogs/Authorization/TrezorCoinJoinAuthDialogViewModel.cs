using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Authorize Coinjoin with Trezor", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class TrezorCoinJoinAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly WalletCoinjoinModel _walletCoinjoinModel;

	// Shown when no bridge is reachable at all: the fix is installing the bridge, not checking the device.
	[AutoNotify] private bool _isBridgeMissing;

	public TrezorCoinJoinAuthDialogViewModel(UiContext uiContext, WalletCoinjoinModel walletCoinjoinModel, WalletType walletType, int maxRounds, decimal maxMiningFeeRate) : base(uiContext)
	{
		_walletCoinjoinModel = walletCoinjoinModel;
		WalletType = walletType;
		LimitsText = $"The device will ask to approve at most {maxRounds} rounds at up to {maxMiningFeeRate:0.##} sat/vByte mining fee rate.";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = $"Authorization failed.{Environment.NewLine}Please, check your device and try again.";

		OpenBridgeDownloadCommand = ReactiveCommand.CreateFromTask(() => UiContext.FileSystem.OpenBrowserAsync(TrezorBridgeManager.SuiteDownloadUrl));
	}

	public WalletType WalletType { get; }

	public string LimitsText { get; }

	public ICommand OpenBridgeDownloadCommand { get; }

	protected override async Task<bool> AuthorizeAsync()
	{
		var authorized = await _walletCoinjoinModel.AuthorizeTrezorAsync().ConfigureAwait(true);
		IsBridgeMissing = !authorized && _walletCoinjoinModel.TrezorAuthorization == TrezorAuthorizationStatus.BridgeNotFound;
		if (!authorized)
		{
			AuthorizationFailedMessage = _walletCoinjoinModel.TrezorAuthorization switch
			{
				TrezorAuthorizationStatus.BridgeNotFound => $"Trezor Bridge is not running.{Environment.NewLine}Start Trezor Suite, which includes the bridge, or download it:",
				TrezorAuthorizationStatus.DeviceNotFound => $"Trezor not found.{Environment.NewLine}Connect and unlock your Trezor, then try again.",
				_ => $"Authorization failed.{Environment.NewLine}Please, check your device and try again.",
			};
		}

		return authorized;
	}
}
