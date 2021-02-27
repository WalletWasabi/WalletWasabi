using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers
{
	public static class AuthorisationHelpers
	{
		public static DialogViewModelBase<SmartTransaction?> GetAuthorisationDialog(Wallet wallet, BuildTransactionResult transaction)
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
