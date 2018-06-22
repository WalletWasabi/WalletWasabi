using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class RecoverWalletViewModel : CategoryViewModel
	{
		private string _password;
		private string _mnemonicWords;
		private string _walletName;
		private bool _termsAccepted;

		public RecoverWalletViewModel() : base("Recover Wallet")
		{
			RecoverCommand = ReactiveCommand.Create(() =>
			{
				WalletName = Guard.Correct(WalletName);
				MnemonicWords = Guard.Correct(MnemonicWords);

				string walletFilePath = Path.Combine(Global.WalletsDir, $"{WalletName}.json");

				if (TermsAccepted == false)
				{
					// ValidationMessage = "Terms are not accepted.";
				}
				else if (string.IsNullOrWhiteSpace(WalletName))
				{
					// ValidationMessage = $"The name {WalletName} is not valid.";
				}
				else if (File.Exists(walletFilePath))
				{
					// ValidationMessage = $"The name {WalletName} is already taken.";
				}
				else if (string.IsNullOrWhiteSpace(MnemonicWords))
				{
					// ValidationMessage = $"Mnemonic words were not supplied.";
				}
				else
				{
					try
					{
						var mnemonic = new Mnemonic(MnemonicWords);
						KeyManager.Recover(mnemonic, Password, walletFilePath);
					}
					catch (Exception ex)
					{
						// ValidationMessage = ex.ToString();
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

		public string MnemonicWords
		{
			get { return _mnemonicWords; }
			set { this.RaiseAndSetIfChanged(ref _mnemonicWords, value); }
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

		public ReactiveCommand RecoverCommand { get; }

		public void OnTermsClicked()
		{
		}

		public void OnPrivacyClicked()
		{
		}
	}
}
