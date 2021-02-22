using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
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
		[AutoNotify] private bool _isPasswordIncorrect;
		[AutoNotify] private bool _isPasswordNeeded;
		[AutoNotify] private string _walletName;

		public LoginViewModel(WalletManagerViewModel walletManagerViewModel, ClosedWalletViewModel closedWalletViewModel)
		{
			var wallet = closedWalletViewModel.Wallet;
			IsPasswordNeeded = !wallet.KeyManager.IsWatchOnly;
			_walletName = wallet.WalletName;
			_password = "";
			WalletIcon = wallet.KeyManager.Icon;
			IsHardwareWallet = wallet.KeyManager.IsHardwareWallet;

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				string? compatibilityPasswordUsed = null;

				IsPasswordIncorrect = !await Task.Run(() => wallet.TryLogin(Password, out compatibilityPasswordUsed));

				if (IsPasswordIncorrect)
				{
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
					Password = "";
				}
			});

			OkCommand = ReactiveCommand.Create(() =>
			{
				Password = "";
				IsPasswordIncorrect = false;
			});

			ForgotPasswordCommand = ReactiveCommand.Create(() =>
				Navigate(NavigationTarget.DialogScreen).To(new PasswordFinderIntroduceViewModel(wallet)));

			EnableAutoBusyOn(NextCommand);
		}

		public string? WalletIcon { get; }

		public bool IsHardwareWallet { get; }

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
