using System;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletSuccessViewModel : CategoryViewModel
	{
		private string _mnemonicWords;
		private string _password;
		private string _validationMessage;


		public GenerateWalletSuccessViewModel(WalletManagerViewModel owner, Mnemonic mnemonic, BitcoinEncryptedSecretNoEC encryptedSecret, string password, string walletFile) 
			: base("Wallet Generated Successfully!")
		{
			_mnemonicWords = mnemonic.ToString();

			ConfirmCommand = ReactiveCommand.Create(() =>
			{
				Password = Guard.Correct(Password); // Don't let whitespaces to the beginning and to the end.

				if (password != Password)
				{
					ValidationMessage = "Password doesn't match";
					return;
				}

				try
				{
					encryptedSecret.GetSecret(Password + "trolo");
					KeyManagement.KeyManager.Recover(mnemonic, Password, walletFile);
					owner.SelectLoadWallet();
				}
				catch (Exception ex)
				{
					ValidationMessage = "Wallet recovery verification failed.";
					Logger.LogError<GenerateWalletSuccessViewModel>(ex);
				}
			});
		}

		public string MnemonicWords
		{
			get { return _mnemonicWords; }
			set { this.RaiseAndSetIfChanged(ref _mnemonicWords, value); }
		}

		public string Password
		{
			get { return _password; }
			set { this.RaiseAndSetIfChanged(ref _password, value); }
		}

		public string ValidationMessage
		{
			get { return _validationMessage; }
			set { this.RaiseAndSetIfChanged(ref _validationMessage, value); }
		}

		public ReactiveCommand ConfirmCommand { get; }

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();
			Password = string.Empty;
		}
	}
}
