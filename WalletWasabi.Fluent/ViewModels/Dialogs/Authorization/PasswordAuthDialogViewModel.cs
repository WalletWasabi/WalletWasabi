using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Enter your passphrase", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class PasswordAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private string _password;

	public PasswordAuthDialogViewModel(IWalletModel wallet, string continueText = "Continue")
	{
		if (wallet.IsHardwareWallet)
		{
			throw new InvalidOperationException("Passphrase authorization is not possible on hardware wallets.");
		}

		ContinueText = continueText;

		_wallet = wallet;
		_password = "";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = $"The passphrase is incorrect.{Environment.NewLine}Please try again.";
	}

	public string ContinueText { get; init; }

	protected override async Task<bool> AuthorizeAsync()
	{
		var success = await _wallet.Auth.TryPasswordAsync(Password);
		Password = "";
		return success;
	}
}
