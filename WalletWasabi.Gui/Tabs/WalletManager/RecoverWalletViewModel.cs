using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		private string _validationMessage;
		private string _suggestions;

		public RecoverWalletViewModel(WalletManagerViewModel owner) : base("Recover Wallet")
		{
			RecoverCommand = ReactiveCommand.Create(() =>
			{
				WalletName = Guard.Correct(WalletName);
				MnemonicWords = Guard.Correct(MnemonicWords);
				Password = Guard.Correct(Password); // Don't let whitespaces to the beginning and to the end.

				string walletFilePath = Path.Combine(Global.WalletsDir, $"{WalletName}.json");

				if (TermsAccepted == false)
				{
					ValidationMessage = "Terms are not accepted.";
				}
				else if (string.IsNullOrWhiteSpace(WalletName))
				{
					ValidationMessage = $"The name {WalletName} is not valid.";
				}
				else if (File.Exists(walletFilePath))
				{
					ValidationMessage = $"The name {WalletName} is already taken.";
				}
				else if (string.IsNullOrWhiteSpace(MnemonicWords))
				{
					ValidationMessage = $"Mnemonic words were not supplied.";
				}
				else
				{
					try
					{
						var mnemonic = new Mnemonic(MnemonicWords);
						KeyManager.Recover(mnemonic, Password, walletFilePath);

						owner.SelectLoadWallet();
					}
					catch (Exception ex)
					{
						ValidationMessage = ex.ToTypeMessageString();
						Logger.LogError<LoadWalletViewModel>(ex);
					}
				}
			},
			this.WhenAnyValue(x => x.TermsAccepted));
			this.WhenAnyValue(x => x.MnemonicWords).Subscribe(x => UpdateSuggestions(x));
			this.WhenAnyValue(x => x.Password).Subscribe(x =>
			{
				if (x.NotNullAndNotEmpty())
				{
					char lastChar = x.Last();
					if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
					{
						Password = x.TrimEnd('\r', '\n');
					}
				}
			});
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

		public string Suggestions
		{
			get { return _suggestions; }
			set { this.RaiseAndSetIfChanged(ref _suggestions, value); }
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

		public ReactiveCommand RecoverCommand { get; }

		public void OnTermsClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new TermsAndConditionsViewModel());
		}

		public void OnPrivacyClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new PrivacyPolicyViewModel());
		}

		public void OnLegalClicked()
		{
			IoC.Get<IShell>().AddOrSelectDocument(() => new LegalIssuesViewModel());
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = null;
			MnemonicWords = null;
			WalletName = Utils.GetNextWalletName();
			TermsAccepted = false;
			ValidationMessage = null;
		}

		private void UpdateSuggestions(string words)
		{
			if (string.IsNullOrEmpty(words))
				return;

			string[] enteredWordList = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var lastWorld = enteredWordList.LastOrDefault().Replace("\t", "");

			if (lastWorld.Length < 1)
			{
				Suggestions = string.Empty;
				return;
			}

			var suggestedWords = EnglishWords.Where(w => w.StartsWith(lastWorld));
			Suggestions = string.Join("   ", suggestedWords.ToArray());
		}

		private static IEnumerable<string> EnglishWords = Wordlist.English.GetWords();
	}
}
