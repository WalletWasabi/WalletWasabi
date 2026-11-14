using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(Title = "Enter your passphrase", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class PasswordAuthDialogViewModel : AuthorizationDialogBase
{
	private static readonly string IncorrectPassphrase = $"The passphrase is incorrect.{Environment.NewLine}Please try again.";

	private readonly IWalletModel _wallet;
	[AutoNotify] private string _password;

	public PasswordAuthDialogViewModel(UiContext uiContext, IWalletModel wallet, string continueText = "Continue") : base(uiContext)
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

		AuthorizationFailedMessage = IncorrectPassphrase;
	}

	public string ContinueText { get; init; }

	protected override async Task<bool> AuthorizeAsync()
	{
		if (PasswordHelper.IsTrimmable(Password))
		{
			AuthorizationFailedMessage = PasswordHelper.WhitespaceMessage;
			return false;
		}

		var success = await _wallet.Auth.TryPasswordAsync(Password);
		AuthorizationFailedMessage = IncorrectPassphrase;
		Password = "";
		return success;
	}
}
