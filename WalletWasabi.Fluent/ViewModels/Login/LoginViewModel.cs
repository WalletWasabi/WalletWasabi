using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public partial class LoginViewModel : RoutableViewModel
	{
		[AutoNotify] private string _password;
		[AutoNotify] private bool _isPasswordIncorrect;
		[AutoNotify] private bool _isPasswordNeeded;
		[AutoNotify] private WalletViewModelBase? _selectedWallet;
		[AutoNotify] private string _walletName;

		public LoginViewModel(WalletViewModelBase wallet, WalletManager walletManager)
		{
			SelectedWallet = wallet;
			_password = "";
			_walletName = wallet.WalletName;

			this.WhenAnyValue(x => x.SelectedWallet)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(selectedWallet =>
				{
					if (selectedWallet is { })
					{
						Password = "";
						IsPasswordNeeded = !selectedWallet.Wallet.KeyManager.IsWatchOnly;
					}
				});

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (SelectedWallet is null)
				{
					return;
				}

				IsBusy = true;

				var wallet = walletManager.GetWalletByName(SelectedWallet.WalletName);
				var keyManager = wallet.KeyManager;

				IsPasswordIncorrect = await Task.Run(
					() =>
					{
						if (!IsPasswordNeeded)
						{
							return false;
						}

						if (PasswordHelper.TryPassword(keyManager, Password, out var compatibilityPasswordUsed))
						{
							if (compatibilityPasswordUsed is { })
							{
								// TODO: User should create a new wallet
							}

							return false;
						}

						return true;
					});

				if (!IsPasswordIncorrect)
				{

					if (wallet.State == WalletState.Uninitialized)
					{
						// Task.Run(async () => await LoadWalletAsync(keyManager, walletManager));
						_ = LoadWalletAsync(keyManager, walletManager);
					}

					wallet.Login();

					Navigate().Clear();

					// TODO: navigate to the wallet welcome page
					Navigate(NavigationTarget.HomeScreen).To(SelectedWallet);
				}

				IsBusy = false;
			});

			OkCommand = ReactiveCommand.Create(() =>
			{
				Password = "";
				IsPasswordIncorrect = false;
			});
		}

		public ICommand OkCommand { get; }

		public async Task LoadWalletAsync(KeyManager keyManager, WalletManager walletManager)
		{
			try
			{
				await walletManager.StartWalletAsync(keyManager);
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				await ShowErrorAsync($"We were unable to load your wallet.", ex.ToUserFriendlyString());
				Logger.LogError(ex);
			}
		}
	}
}