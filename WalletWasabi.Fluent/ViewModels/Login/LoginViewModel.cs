using System.Reactive.Concurrency;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	[NavigationMetaData(Title = "Login")]
	public partial class LoginViewModel : RoutableViewModel
	{
		[AutoNotify] private string _password;
		[AutoNotify] private bool _isPasswordNeeded;
		[AutoNotify] private string _errorMessage;

		public LoginViewModel(WalletManagerViewModel walletManagerViewModel, ClosedWalletViewModel closedWalletViewModel)
		{
			var wallet = closedWalletViewModel.Wallet;
			IsPasswordNeeded = !wallet.KeyManager.IsWatchOnly;
			WalletName = wallet.WalletName;
			_password = "";
			_errorMessage = "";
			WalletIcon = wallet.KeyManager.Icon;
			IsHardwareWallet = wallet.KeyManager.IsHardwareWallet;

			NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(walletManagerViewModel, closedWalletViewModel, wallet));

			OkCommand = ReactiveCommand.Create(OnOk);

			ForgotPasswordCommand = ReactiveCommand.Create(() => OnForgotPassword(wallet));

			EnableAutoBusyOn(NextCommand);
		}

		private async Task OnNextAsync(WalletManagerViewModel walletManagerViewModel, ClosedWalletViewModel closedWalletViewModel, Wallet wallet)
		{
			string? compatibilityPasswordUsed = null;

			var isPasswordCorrect = await Task.Run(() => wallet.TryLogin(Password, out compatibilityPasswordUsed));

			if (!isPasswordCorrect)
			{
				ErrorMessage = "The password is incorrect! Try Again.";
				return;
			}

			if (compatibilityPasswordUsed is { })
			{
				await ShowErrorAsync(Title, PasswordHelper.CompatibilityPasswordWarnMessage, "Compatibility password was used");
			}

			var legalResult = await ShowLegalAsync();

			if (legalResult)
			{
				LoginWallet(walletManagerViewModel, closedWalletViewModel);
			}
			else
			{
				wallet.Logout();
				ErrorMessage = "You must accept the Terms and Conditions!";
			}
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

		public string? WalletIcon { get; }

		public bool IsHardwareWallet { get; }

		public string WalletName { get; }

		public ICommand OkCommand { get; }

		public ICommand ForgotPasswordCommand { get; }

		private void LoginWallet(WalletManagerViewModel walletManagerViewModel, ClosedWalletViewModel closedWalletViewModel)
		{
			closedWalletViewModel.RaisePropertyChanged(nameof(WalletViewModelBase.IsLoggedIn));
			RxApp.MainThreadScheduler.Schedule(async () => await walletManagerViewModel.LoadWalletAsync(closedWalletViewModel));
			Navigate().To(closedWalletViewModel, NavigationMode.Clear);
		}

		private async Task<bool> ShowLegalAsync()
		{
			if (!Services.LegalChecker.TryGetNewLegalDocs(out var document))
			{
				return true;
			}

			var legalDocs = new TermsAndConditionsViewModel(document.Content);

			var dialogResult = await NavigateDialogAsync(legalDocs, NavigationTarget.DialogScreen);

			if (dialogResult.Result)
			{
				await Services.LegalChecker.AgreeAsync();
			}

			return dialogResult.Result;
		}
	}
}
