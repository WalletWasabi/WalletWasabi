using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public static class Interactions
{
	public static readonly Interaction<AuthorizationRequest, bool> TransactionAuthorize = new();
	public static readonly Interaction<Wallet, bool> LightAuthorize = new();

	static Interactions()
	{
		TransactionAuthorize.RegisterHandler(HandleAuthorizationInteractionAsync);
		LightAuthorize.RegisterHandler(HandlePeekAuthorizeInteractionAsync);
	}

	private static async Task HandlePeekAuthorizeInteractionAsync(InteractionContext<Wallet, bool> interaction)
	{
		var softwareWalletAuthorization = await SoftwareWalletAuthorization(interaction.Input);
		interaction.SetOutput(softwareWalletAuthorization);
	}

	private static async Task HandleAuthorizationInteractionAsync(InteractionContext<AuthorizationRequest, bool> interaction)
	{
		var request = interaction.Input;
		bool isAuthorized;
		if (request.IsHardwareWallet)
		{
			isAuthorized = await HardwareWalletAuthorization(request.Wallet, request.TransactionAuthorizationInfo);
		}
		else
		{
			isAuthorized = await SoftwareWalletAuthorization(request.Wallet);
		}

		interaction.SetOutput(isAuthorized);
	}

	private static async Task<bool> HardwareWalletAuthorization(Wallet wallet,
		TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		var dialog = new HardwareWalletAuthDialogViewModel(wallet, transactionAuthorizationInfo);
		var result = await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(dialog);

		if (result.Result && result.Kind == DialogResultKind.Normal)
		{
			return true;
		}

		if (!result.Result && result.Kind == DialogResultKind.Normal)
		{
			await ShowErrorAsync("Authorization", "The Authorization has failed, please try again.", "");
		}

		return false;
	}

	private static async Task<bool> SoftwareWalletAuthorization(Wallet wallet)
	{
		if (string.IsNullOrEmpty(wallet.Kitchen.SaltSoup()))
		{
			return true;
		}

		bool authorized = false;
		bool retry;
		do
		{
			var dialog = new PasswordAuthDialogViewModel(wallet);
			var dialogResult = await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(dialog);

			if (dialogResult.Result && dialogResult.Kind == DialogResultKind.Normal)
			{
				return true;
			}

			if (dialogResult.Kind == DialogResultKind.Normal)
			{
				await ShowErrorAsync("Wallet Info", "The password is incorrect! Try Again.", "");
				retry = true;
			}
			else
			{
				retry = false;
			}

		} while (retry);

		return authorized;
	}

	private static async Task ShowErrorAsync(string title, string message, string caption)
	{
		var dialog = new ShowErrorDialogViewModel(message, title, caption);
		await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(dialog);
	}
}