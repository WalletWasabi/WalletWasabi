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
			LegalChecker = walletManagerViewModel.LegalChecker;
			KeyManager = closedWalletViewModel.Wallet.KeyManager;
			IsPasswordNeeded = !wallet.KeyManager.IsWatchOnly;
			_walletName = wallet.WalletName;
			_password = "";

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				string? compatibilityPasswordUsed = null;

				IsPasswordIncorrect = !await Task.Run(() => closedWalletViewModel.Wallet.TryLogin(Password, out compatibilityPasswordUsed));

				if (IsPasswordIncorrect)
				{
					return;
				}

				if (compatibilityPasswordUsed is { })
				{
					await ShowErrorAsync(PasswordHelper.CompatibilityPasswordWarnMessage, "Compatibility password was used");
				}

				var legalResult = await ShowLegalAsync();

				if (legalResult)
				{
					await LoginWalletAsync(walletManagerViewModel, closedWalletViewModel);
				}
				else
				{
					closedWalletViewModel.Wallet.Logout();
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

		public LegalChecker LegalChecker { get; }

		public KeyManager KeyManager { get; }

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

		private async Task<bool> ShowLegalAsync()
		{
			if (!LegalChecker.TryGetNewLegalDocs(out var document))
			{
				return true;
			}

			var legalDocs = new TermsAndConditionsViewModel(document.Content);

			var dialogResult = await NavigateDialog(legalDocs, NavigationTarget.DialogScreen);

			if (dialogResult.Result)
			{
				await LegalChecker.AgreeAsync();
			}

			return dialogResult.Result;
		}
	}
}
