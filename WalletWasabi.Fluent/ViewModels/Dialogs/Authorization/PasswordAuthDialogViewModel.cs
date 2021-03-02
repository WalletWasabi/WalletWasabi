using System.Threading.Tasks;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization
{
	[NavigationMetaData(Title = "Enter your password")]
	public partial class PasswordAuthDialogViewModel : AuthorizationDialogBase
	{
		private readonly Wallet _wallet;
		[AutoNotify] private string _password;

		public PasswordAuthDialogViewModel(Wallet wallet)
		{
			_wallet = wallet;
			_password = "";
		}

		protected override void OnDialogClosed()
		{
			Password = "";
		}

		protected override async Task<bool> Authorize()
		{
			return await Task.Run(() => PasswordHelper.TryPassword(_wallet.KeyManager, Password, out _));
		}
	}
}
