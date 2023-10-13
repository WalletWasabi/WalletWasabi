using System.Threading.Tasks;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Authorize with Hardware Wallet", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class HardwareWalletAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly IHardwareWalletModel _wallet;
	private readonly TransactionAuthorizationInfo _transactionAuthorizationInfo;

	public HardwareWalletAuthDialogViewModel(IHardwareWalletModel wallet, TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		_wallet = wallet;
		_transactionAuthorizationInfo = transactionAuthorizationInfo;
		WalletType = wallet.Settings.WalletType;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = $"Authorization failed.{Environment.NewLine}Please, check your device and try again.";
	}

	public WalletType WalletType { get; }

	protected override Task<bool> AuthorizeAsync()
	{
		return _wallet.AuthorizeTransactionAsync(_transactionAuthorizationInfo);
	}
}
