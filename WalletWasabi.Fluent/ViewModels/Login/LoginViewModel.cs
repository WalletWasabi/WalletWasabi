using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login;

[NavigationMetaData(Title = "")]
public partial class LoginViewModel : RoutableViewModel
{
	[ObservableProperty] private string _password;
	[ObservableProperty] private bool _isPasswordNeeded;
	[ObservableProperty] private string _errorMessage;
	[ObservableProperty] private bool _isForgotPasswordVisible;

	public LoginViewModel(ClosedWalletViewModel closedWalletViewModel)
	{
		var wallet = closedWalletViewModel.Wallet;
		IsPasswordNeeded = !wallet.KeyManager.IsWatchOnly;
		WalletName = wallet.WalletName;
		_password = "";
		_errorMessage = "";
		WalletType = WalletHelpers.GetType(closedWalletViewModel.Wallet.KeyManager);

		NextCommand = new AsyncRelayCommand(async () => await OnNextAsync(closedWalletViewModel, wallet));

		OkCommand = new RelayCommand(OnOk);

		ForgotPasswordCommand = new RelayCommand(() => OnForgotPassword(wallet));

		// EnableAutoBusyOn(NextCommand);
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	public ICommand OkCommand { get; }

	public ICommand ForgotPasswordCommand { get; }

	private async Task OnNextAsync(ClosedWalletViewModel closedWalletViewModel, Wallet wallet)
	{
		IsBusy = true;

		string? compatibilityPasswordUsed = null;

		var isPasswordCorrect = await Task.Run(() => wallet.TryLogin(Password, out compatibilityPasswordUsed));

		if (!isPasswordCorrect)
		{
			IsForgotPasswordVisible = true;
			ErrorMessage = "The password is incorrect! Please try again.";
			return;
		}

		if (compatibilityPasswordUsed is { })
		{
			await ShowErrorAsync(Title, PasswordHelper.CompatibilityPasswordWarnMessage, "Compatibility password was used");
		}

		var legalResult = await ShowLegalAsync();

		if (legalResult)
		{
			LoginWallet(closedWalletViewModel);
		}
		else
		{
			wallet.Logout();
			ErrorMessage = "You must accept the Terms and Conditions!";
		}

		IsBusy = false;
	}

	private void OnOk()
	{
		Password = "";
		ErrorMessage = "";
	}

	private void OnForgotPassword(Wallet wallet)
	{
		Navigate(NavigationTarget.DialogScreen).To(new PasswordFinderIntroduceViewModel(wallet));
	}

	private void LoginWallet(ClosedWalletViewModel closedWalletViewModel)
	{
		closedWalletViewModel.NotifyPropertyChanged(nameof(WalletViewModelBase.IsLoggedIn));
		closedWalletViewModel.StartLoading();

		if (closedWalletViewModel.IsSelected /*&& closedWalletViewModel.OpenCommand.CanExecute(default)*/) // TODO RelayCommand: parameter for canExecute cannot be null. Maybe method also needs to be provided (?)
		{
			closedWalletViewModel.OpenCommand.Execute(true);
		}
	}

	private async Task<bool> ShowLegalAsync()
	{
		if (!Services.LegalChecker.TryGetNewLegalDocs(out _))
		{
			return true;
		}

		var legalDocs = new TermsAndConditionsViewModel();

		var dialogResult = await NavigateDialogAsync(legalDocs, NavigationTarget.DialogScreen);

		if (dialogResult.Result)
		{
			await Services.LegalChecker.AgreeAsync();
		}

		return dialogResult.Result;
	}
}
