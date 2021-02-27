using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers
{
	public static class AuthorisationHelpers
	{
		public static DialogViewModelBase<bool> GetAuthorisationDialog(Wallet wallet, ref BuildTransactionResult transaction)
		{
			if (wallet.KeyManager.IsHardwareWallet)
			{
				return new HardwareWalletAuthDialog(wallet, ref transaction);
			}
			else
			{
				return new PasswordAuthDialogViewModel(wallet);
			}
		}
	}
}
