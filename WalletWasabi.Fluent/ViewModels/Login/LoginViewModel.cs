using System;
using System.Threading.Tasks;
using ReactiveUI;
using Splat;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public class LoginViewModel : RoutableViewModel
	{
		public LoginViewModel(WalletViewModelBase wallet)
		{
			Password = "";

			this.ValidateProperty(x => x.Password, ValidatePassword);

			NextCommand = ReactiveCommand.Create(() =>
			{
				// TODO: isBusy
				var walletManager = Locator.Current.GetService<Global>().WalletManager;

				KeyManager keyManager = walletManager.GetWalletByName(wallet.WalletName).KeyManager;



				if (PasswordHelper.TryPassword(keyManager, Password, out var compatibilityPasswordUsed))
				{
					if (compatibilityPasswordUsed is { })
					{
						// TODO: User should create a new wallet
					}

					Task.Run(async () => await LoadWalletAsync(keyManager, walletManager));

					// TODO: navigate to the wallet welcome page
					Navigate().To(wallet);
				}
			});
		}

		private void ValidatePassword(IValidationErrors errors)
		{
			throw new NotImplementedException();
		}

		public string Password { get; set; }

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