using System.Threading.Tasks;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "HardwareWalletAuthDialogViewModel_Title", NavigationTarget = NavigationTarget.CompactDialogScreen)]
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

		AuthorizationFailedMessage = Lang.Resources.HardwareWalletAuthDialogViewModel_Error_AuthorizationFailed;
	}

	public WalletType WalletType { get; }

	protected override Task<bool> AuthorizeAsync()
	{
		return _wallet.AuthorizeTransactionAsync(_transactionAuthorizationInfo);
	}
}
