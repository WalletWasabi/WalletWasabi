using System.Threading.Tasks;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Enter your password", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class PasswordAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly Wallet _wallet;
	[AutoNotify] private string _password;

	public PasswordAuthDialogViewModel(Wallet wallet)
	{
		if (wallet.KeyManager.IsHardwareWallet)
		{
			throw new InvalidOperationException("Password authorization is not possible on hardware wallets.");
		}

		_wallet = wallet;
		_password = "";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = $"The password is incorrect.{Environment.NewLine}Please try again.";
	}

	protected override async Task<bool> AuthorizeAsync()
	{
		var success = await Task.Run(() => PasswordHelper.TryPassword(_wallet.KeyManager, Password, out _));
		Password = "";
		return success;
	}
}
