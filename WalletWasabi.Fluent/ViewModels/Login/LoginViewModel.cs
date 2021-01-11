﻿using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Login
{
	public partial class LoginViewModel : RoutableViewModel
	{
		[AutoNotify] private string _password;
		[AutoNotify] private bool _isPasswordIncorrect;
		[AutoNotify] private bool _isPasswordNeeded;
		[AutoNotify] private string _walletName;

		public LoginViewModel(WalletViewModelBase walletViewModelBase)
		{
			Title = "Login";
			KeyManager = walletViewModelBase.Wallet.KeyManager;
			IsPasswordNeeded = !KeyManager.IsWatchOnly;
			_walletName = walletViewModelBase.WalletName;
			_password = "";

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var wallet = walletViewModelBase.Wallet;

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
					wallet.Login();

					// TODO: navigate to the wallet welcome page
					Navigate().To(walletViewModelBase, NavigationMode.Clear);
				}
			});

			OkCommand = ReactiveCommand.Create(() =>
			{
				Password = "";
				IsPasswordIncorrect = false;
			});

			EnableAutoBusyOn(NextCommand);
		}

		public ICommand OkCommand { get; }

		public KeyManager KeyManager { get; }
	}
}