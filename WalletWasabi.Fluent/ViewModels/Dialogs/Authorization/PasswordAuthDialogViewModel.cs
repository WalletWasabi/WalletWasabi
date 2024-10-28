using System.Threading.Tasks;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class PasswordAuthDialogViewModel : AuthorizationDialogBase
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private string _password;

	public PasswordAuthDialogViewModel(IWalletModel wallet, string? continueText = null)
	{
		continueText ??= Lang.Resources.Words_Continue;

		if (wallet.IsHardwareWallet)
		{
			throw new InvalidOperationException("Passphrase authorization is not possible on hardware wallets.");
		}

		Title = Lang.Resources.PasswordAuthDialogViewModel_Title;

		ContinueText = continueText;

		_wallet = wallet;
		_password = "";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		AuthorizationFailedMessage = Lang.Resources.PasswordAuthDialogViewModel_Error_AuthorizationFailed_Message;
	}

	public string ContinueText { get; init; }

	protected override async Task<bool> AuthorizeAsync()
	{
		var success = await _wallet.Auth.TryPasswordAsync(Password);
		Password = "";
		return success;
	}
}
