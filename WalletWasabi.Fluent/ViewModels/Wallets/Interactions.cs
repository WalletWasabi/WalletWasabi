using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public static class Interactions
{
	public static readonly Interaction<Wallet, bool> Authorize = new();

	static Interactions()
	{
		Authorize.RegisterHandler(HandleAuthorizationInteractionAsync);
	}

	private static async Task HandleAuthorizationInteractionAsync(InteractionContext<Wallet, bool> interaction)
	{
		bool authorized = false;
		if (!string.IsNullOrEmpty(interaction.Input.Kitchen.SaltSoup()))
		{
			bool retry;
			do
			{
				var dialog = new PasswordAuthDialogViewModel(interaction.Input);
				var dialogResult = await MainViewModel.Instance.CompactDialogScreen.NavigateDialogAsync(dialog);

				if (dialogResult.Result && dialogResult.Kind == DialogResultKind.Normal)
				{
					retry = false;
					authorized = true;
				}
				else if (dialogResult.Kind == DialogResultKind.Normal)
				{
					await ShowErrorAsync("Wallet Info", "The password is incorrect! Try Again.", "");
					retry = true;
				}
				else
				{
					retry = false;
				}
			} while (retry);

			interaction.SetOutput(authorized);
		}
		else
		{
			interaction.SetOutput(true);
		}
	}

	private static async Task ShowErrorAsync(string title, string message, string caption)
	{
		var dialog = new ShowErrorDialogViewModel(message, title, caption);
		await MainViewModel.Instance.DialogScreen.NavigateDialogAsync(dialog);
	}
}