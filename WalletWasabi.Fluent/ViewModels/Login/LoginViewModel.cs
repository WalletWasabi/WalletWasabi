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
		private readonly WalletManagerViewModel _walletManagerViewModel;
		private readonly ClosedWalletViewModel _closedWalletVm;

		[AutoNotify] private string _password;
		[AutoNotify] private bool _isPasswordIncorrect;
		[AutoNotify] private bool _isPasswordNeeded;
		[AutoNotify] private string _walletName;

		public LoginViewModel(WalletManagerViewModel walletManagerViewModel, ClosedWalletViewModel closedWalletVm, LegalChecker legalChecker)
		{
			_walletManagerViewModel = walletManagerViewModel;
			_closedWalletVm = closedWalletVm;

			var wallet = _closedWalletVm.Wallet;

			KeyManager = wallet.KeyManager;
			IsPasswordNeeded = !KeyManager.IsWatchOnly;

			_walletName = wallet.WalletName;
			_password = "";

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				IsPasswordIncorrect = await Task.Run(async () =>
				{
					if (!IsPasswordNeeded)
					{
						return false;
					}

					if (PasswordHelper.TryPassword(KeyManager, Password, out var compatibilityPasswordUsed))
					{
						if (compatibilityPasswordUsed is { })
						{
							await ShowErrorAsync(PasswordHelper.CompatibilityPasswordWarnMessage, "Compatibility password was used");
						}

						return false;
					}

					return true;
				});

				if (!IsPasswordIncorrect)
				{
					if (legalChecker.TryGetNewLegalDocs(out var document))
					{
						var legalDocs = new TermsAndConditionsViewModel(document.Content);

						var dialogResult = await NavigateDialog(legalDocs, NavigationTarget.DialogScreen);

						if (dialogResult.Result)
						{
							await legalChecker.AgreeAsync();
							await LoginWallet();
						}
					}
					else
					{
						await LoginWallet();
					}
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

		public ICommand OkCommand { get; }

		public ICommand ForgotPasswordCommand { get; }

		public KeyManager KeyManager { get; }

		private async Task LoginWallet()
		{
			_closedWalletVm.Wallet.Login();
			_closedWalletVm.RaisePropertyChanged(nameof(WalletViewModelBase.IsLoggedIn));

			var destination = await _walletManagerViewModel.LoginWalletAsync(_closedWalletVm);

			if (destination is { })
			{
				Navigate().To(destination, NavigationMode.Clear);
			}
			else
			{
				await ShowErrorAsync("Error", "Wasabi was unable to login and load your wallet.");
			}
		}
	}
}
