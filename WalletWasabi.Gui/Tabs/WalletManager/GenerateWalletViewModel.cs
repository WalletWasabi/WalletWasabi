using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class GenerateWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _passwordConfirmation;
		private string _walletName;
		private bool _termsAccepted;

		public GenerateWalletViewModel() : base("Generate Wallet")
		{
			GenerateCommand = ReactiveCommand.Create(() =>
			{
				string walletFilePath = Path.Combine(Global.WalletsDir, $"{WalletName}.json");

				if (TermsAccepted == false)
				{
					// Terms are not accepted.
				}
				else if (string.IsNullOrWhiteSpace(WalletName))
				{
					// Invalid wallet name.
				}
				else if (File.Exists(walletFilePath))
				{
					// Wallet with the same name already exists.
				}
				else if (Password != PasswordConfirmation)
				{
					// Password does not match the password confirmation.
				}
				else
				{
					try
					{
						KeyManager.CreateNew(out Mnemonic mnemonic, Password, walletFilePath);
						// https://imgur.com/a/PTkQJJN
					}
					catch (Exception ex)
					{
						// ex.ToString()
					}
				}
			},
			this.WhenAnyValue(x => x.TermsAccepted));
		}

		public string Password
		{
			get { return _password; }
			set { this.RaiseAndSetIfChanged(ref _password, value); }
		}

		public string PasswordConfirmation
		{
			get { return _passwordConfirmation; }
			set { this.RaiseAndSetIfChanged(ref _passwordConfirmation, value); }
		}

		public string WalletName
		{
			get { return _walletName; }
			set { this.RaiseAndSetIfChanged(ref _walletName, value); }
		}

		public bool TermsAccepted
		{
			get { return _termsAccepted; }
			set { this.RaiseAndSetIfChanged(ref _termsAccepted, value); }
		}

		public ReactiveCommand GenerateCommand { get; }

		public void OnTermsClicked()
		{
		}

		public void OnPrivacyClicked()
		{
		}
	}
}
