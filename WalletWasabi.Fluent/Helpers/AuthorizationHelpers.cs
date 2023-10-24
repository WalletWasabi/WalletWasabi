using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

// TODO: Remove this entire class after SendViewModel is decoupled.
public static class AuthorizationHelpers
{
	public static AuthorizationDialogBase GetAuthorizationDialog(IWalletModel wallet, TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		if (wallet is IHardwareWalletModel hwm)
		{
			return new HardwareWalletAuthDialogViewModel(hwm, transactionAuthorizationInfo);
		}
		else
		{
			return new PasswordAuthDialogViewModel(wallet, "Send");
		}
	}
}
