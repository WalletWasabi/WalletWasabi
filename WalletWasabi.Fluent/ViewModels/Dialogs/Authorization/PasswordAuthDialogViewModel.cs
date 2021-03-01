using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization
{
	[NavigationMetaData(Title = "Enter your password")]
	public partial class PasswordAuthDialogViewModel : AuthorizationDialogBase
	{
		private readonly Wallet _wallet;
		private readonly SmartTransaction _transaction;
		[AutoNotify] private string _password;

		public PasswordAuthDialogViewModel(Wallet wallet, SmartTransaction transaction)
		{
			_wallet = wallet;
			_transaction = transaction;
			_password = "";
		}

		protected override void OnDialogClosed()
		{
			Password = "";
		}

		protected override async Task Authorize()
		{
			var passwordValid = await Task.Run(() => PasswordHelper.TryPassword(_wallet.KeyManager, Password, out _));

			if (passwordValid)
			{
				Close(DialogResultKind.Normal, _transaction);
			}
			else
			{
				await ShowErrorAsync("Password", "Password was incorrect.", "");
				Close();
			}
		}
	}
}
