using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Services;
using WalletWasabi.Userfacing;

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

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
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
					await ShowErrorAsync(PasswordHelper.CompatibilityPasswordWarnMessage, "Compatibility password was used");
				}

				var legalResult = await ShowLegalAsync(walletManagerViewModel.LegalChecker);

				if (legalResult)
				{
					await LoginWalletAsync(walletManagerViewModel, closedWalletViewModel);
				}
				else
				{
					wallet.Logout();
					ErrorMessage = "You must accept the Terms and Conditions!";
				}
			});

			OkCommand = ReactiveCommand.Create(() =>
			{
				Password = "";
				ErrorMessage = "";
			});

			ForgotPasswordCommand = ReactiveCommand.Create(() =>
				Navigate(NavigationTarget.DialogScreen).To(new PasswordFinderIntroduceViewModel(wallet)));

			EnableAutoBusyOn(NextCommand);
		}

		public string? WalletIcon { get; }

		public bool IsHardwareWallet { get; }

		public string WalletName { get; }

		public ICommand OkCommand { get; }

		public ICommand ForgotPasswordCommand { get; }

		private async Task LoginWalletAsync(WalletManagerViewModel walletManagerViewModel, ClosedWalletViewModel closedWalletViewModel)
		{
			closedWalletViewModel.RaisePropertyChanged(nameof(WalletViewModelBase.IsLoggedIn));

			var destination = await walletManagerViewModel.LoadWalletAsync(closedWalletViewModel);

			if (destination is { })
			{
				Navigate().To(destination, NavigationMode.Clear);
			}
			else
			{
				await ShowErrorAsync("Error", "Wasabi was unable to login and load your wallet.");
			}
		}

		private async Task<bool> ShowLegalAsync(LegalChecker legalChecker)
		{
			if (!legalChecker.TryGetNewLegalDocs(out var document))
			{
				return true;
			}

			var legalDocs = new TermsAndConditionsViewModel(document.Content);

			var dialogResult = await NavigateDialog(legalDocs, NavigationTarget.DialogScreen);

			if (dialogResult.Result)
			{
				await legalChecker.AgreeAsync();
			}

			return dialogResult.Result;
		}
	}
}
