using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Enter your password", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class PasswordAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private string _password;

	public PasswordAuthDialogViewModel(IWalletModel wallet, PasswordRequestIntent intent)
	{
		if (wallet.IsHardwareWallet)
		{
			throw new InvalidOperationException("Password authorization is not possible on hardware wallets.");
		}

		_wallet = wallet;
		Intent = intent;

		_password = "";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = $"The password is incorrect.{Environment.NewLine}Please try again.";
	}

	public string ContinueText => Intent == PasswordRequestIntent.Send ? "SEND" : "Next";

	public PasswordRequestIntent Intent { get; }

	protected override async Task<bool> AuthorizeAsync()
	{
		var success = await _wallet.Auth.TryPasswordAsync(Password);
		Password = "";
		return success;
	}
}

// Source generators don't support nested classes, otherwise, this would be inside the class above.
public enum PasswordRequestIntent
{
	Invalid = 0,
	Send,
	Other,
}
