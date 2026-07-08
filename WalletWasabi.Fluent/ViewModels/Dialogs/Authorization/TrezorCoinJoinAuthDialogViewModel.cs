using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Authorize Coinjoin with Trezor", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class TrezorCoinJoinAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly WalletCoinjoinModel _walletCoinjoinModel;

	public TrezorCoinJoinAuthDialogViewModel(UiContext uiContext, WalletCoinjoinModel walletCoinjoinModel, WalletType walletType, int maxRounds, decimal maxMiningFeeRate) : base(uiContext)
	{
		_walletCoinjoinModel = walletCoinjoinModel;
		WalletType = walletType;
		LimitsText = $"The device will ask to approve at most {maxRounds} rounds at up to {maxMiningFeeRate:0.##} sat/vByte mining fee rate.";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = $"Authorization failed.{Environment.NewLine}Please, check your device and try again.";
	}

	public WalletType WalletType { get; }

	public string LimitsText { get; }

	protected override async Task<bool> AuthorizeAsync()
	{
		var authorized = await _walletCoinjoinModel.AuthorizeTrezorAsync().ConfigureAwait(true);
		if (!authorized)
		{
			AuthorizationFailedMessage = _walletCoinjoinModel.TrezorAuthorization == TrezorAuthorizationStatus.DeviceNotFound
				? $"Trezor not found.{Environment.NewLine}Connect and unlock your Trezor, then try again."
				: $"Authorization failed.{Environment.NewLine}Please, check your device and try again.";
		}

		return authorized;
	}
}
