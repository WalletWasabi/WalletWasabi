using System;
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
	public abstract partial class LoginViewModelBase : RoutableViewModel
	{
		[AutoNotify] private string _password;
		[AutoNotify] private bool _isPasswordIncorrect;
		[AutoNotify] private WalletViewModelBase? _selectedWallet;

		protected LoginViewModelBase(WalletManager walletManager)
		{
			_password = "";

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (SelectedWallet is null)
				{
					return;
				}

				IsBusy = true;

				KeyManager keyManager = walletManager.GetWalletByName(SelectedWallet.WalletName).KeyManager;

				IsPasswordIncorrect = await Task.Run(
					() =>
					{
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
					// Task.Run(async () => await LoadWalletAsync(keyManager, walletManager));
					_ = LoadWalletAsync(keyManager, walletManager);

					// TODO: navigate to the wallet welcome page
					Navigate().To(SelectedWallet);
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