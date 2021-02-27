using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers
{
	public static class AuthorizationHelpers
	{
		public static AuthorizationDialogBase GetAuthorizationDialog(Wallet wallet, BuildTransactionResult transaction)
		{
			if (wallet.KeyManager.IsHardwareWallet)
			{
				return new HardwareWalletAuthDialogViewModel(wallet, transaction);
			}
			else
			{
				return new PasswordAuthDialogViewModel(wallet, transaction.Transaction);
			}
		}
	}
}
