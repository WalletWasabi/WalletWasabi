using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login;

[NavigationMetaData(Title = "")]
public partial class LoginViewModel : RoutableViewModel
{
	[AutoNotify] private string _password;
	[AutoNotify] private bool _isPasswordNeeded;
	[AutoNotify] private string _errorMessage;
	[AutoNotify] private bool _isForgotPasswordVisible;

	// TODO: finish partial refactor
	// Wallet parameter must be removed.
	private LoginViewModel(IWalletModel walletModel, Wallet wallet)
	{
		_password = "";
		_errorMessage = "";
		IsPasswordNeeded = !walletModel.IsWatchOnlyWallet;
		WalletName = walletModel.Name;
		WalletType = walletModel.WalletType;

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(walletModel));

		OkCommand = ReactiveCommand.Create(OnOk);

		ForgotPasswordCommand = ReactiveCommand.Create(() => OnForgotPassword(wallet));

		EnableAutoBusyOn(NextCommand);
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	public ICommand OkCommand { get; }

	public ICommand ForgotPasswordCommand { get; }

	private async Task OnNextAsync(IWalletModel walletModel)
	{
		var (success, compatibilityPasswordUsed) = await walletModel.Auth.TryLoginAsync(Password);

		if (!success)
		{
			IsForgotPasswordVisible = true;
			ErrorMessage = "The password is incorrect! Please try again.";
			return;
		}

		if (compatibilityPasswordUsed)
		{
			await ShowErrorAsync(Title, PasswordHelper.CompatibilityPasswordWarnMessage, "Compatibility password was used");
		}

		if (walletModel.Auth.IsLegalRequired)
		{
			var accepted = await ShowLegalAsync();
			if (accepted)
			{
				await walletModel.Auth.AcceptTermsAndConditions();
				walletModel.Auth.CompleteLogin();
			}
			else
			{
				walletModel.Auth.Logout();
				ErrorMessage = "You must accept the Terms and Conditions!";
			}
		}
		else
		{
			walletModel.Auth.CompleteLogin();
		}
	}

	private void OnOk()
	{
		Password = "";
		ErrorMessage = "";
	}

	private void OnForgotPassword(Wallet wallet)
	{
		UiContext.Navigate(NavigationTarget.DialogScreen).To(new PasswordFinderIntroduceViewModel(wallet));
	}

	private async Task<bool> ShowLegalAsync()
	{
		var legalDocs = new TermsAndConditionsViewModel();

		var dialogResult = await NavigateDialogAsync(legalDocs, NavigationTarget.DialogScreen);

		return dialogResult.Result;
	}
}
