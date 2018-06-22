using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using WalletWasabi.Gui.Dialogs;
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
		private string _validationMessage;

		public GenerateWalletViewModel(WalletManagerViewModel owner) : base("Generate Wallet")
		{
			GenerateCommand = ReactiveCommand.Create(async () =>
			{
				string walletFilePath = Path.Combine(Global.WalletsDir, $"{WalletName}.json");

				if (TermsAccepted == false)
				{
					// Terms are not accepted.
				}
				else if (string.IsNullOrWhiteSpace(WalletName))
				{
					// Invalid wallet name.
					ValidationMessage = $"The name {WalletName} is not valid.";
				}
				else if (File.Exists(walletFilePath))
				{
					// Wallet with the same name already exists.
					ValidationMessage = $"The name {WalletName} is already taken.";
				}
				else if (Password != PasswordConfirmation)
				{
					// Password does not match the password confirmation.
					ValidationMessage = $"The passwords do not match.";
				}
				else
				{
					try
					{
						KeyManager.CreateNew(out Mnemonic mnemonic, Password, walletFilePath);

						owner.SelectedCategory = new GenerateWalletSuccessViewModel();
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

		public string ValidationMessage
		{
			get { return _validationMessage; }
			set { this.RaiseAndSetIfChanged(ref _validationMessage, value); }
		}

		public ReactiveCommand GenerateCommand { get; }

		public void OnTermsClicked()
		{
		}

		public void OnPrivacyClicked()
		{
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = null;
			PasswordConfirmation = null;
			WalletName = GetNextWalletName();
			TermsAccepted = false;
			ValidationMessage = null;
		}

		private static string GetNextWalletName()
		{
			for (int i = 0; i < int.MaxValue; i++)
			{
				if (!File.Exists(Path.Combine(Global.WalletsDir, $"Wallet{i}.json")))
				{
					return $"Wallet{i}";
				}
			}

			throw new NotSupportedException("This is impossible.");
		}
	}
}
