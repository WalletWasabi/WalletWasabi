using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class AuthorizationHelpers
{
	public static AuthorizationDialogBase GetAuthorizationDialog(Wallet wallet, TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		if (wallet.KeyManager.IsHardwareWallet)
		{
			return new HardwareWalletAuthDialogViewModel(wallet, transactionAuthorizationInfo);
		}
		else
		{
			return new PasswordAuthDialogViewModel(wallet);
		}
	}
}
