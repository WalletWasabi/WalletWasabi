using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using WalletWasabi.Gui.Tabs.LegalDocs;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class RecoverWalletViewModel : CategoryViewModel
	{
		private int _caretIndex;
		private string _password;
		private string _mnemonicWords;
		private string _walletName;
		private string _validationMessage;
		private bool _showAdvancedOptions;
		private string _accountKeyPath;
		private int _minGapLimit;
		private ObservableCollection<SuggestionViewModel> _suggestions;
		public Global Global { get; }

		public RecoverWalletViewModel(WalletManagerViewModel owner) : base("Recover Wallet")
		{
			Global = owner.Global;
			MnemonicWords = "";

			RecoverCommand = ReactiveCommand.Create(() =>
				{
					WalletName = Guard.Correct(WalletName);
					MnemonicWords = Guard.Correct(MnemonicWords);
					Password = Guard.Correct(Password); // Do not let whitespaces to the beginning and to the end.

					string walletFilePath = Path.Combine(Global.WalletsDir, $"{WalletName}.json");

					if (string.IsNullOrWhiteSpace(WalletName))
					{
						ValidationMessage = $"The name {WalletName} is not valid.";
					}
					else if (File.Exists(walletFilePath))
					{
						ValidationMessage = $"The name {WalletName} is already taken.";
					}
					else if (string.IsNullOrWhiteSpace(MnemonicWords))
					{
						ValidationMessage = "Recovery Words were not supplied.";
					}
					else if (string.IsNullOrWhiteSpace(AccountKeyPath))
					{
						ValidationMessage = "The account key path is not valid.";
					}
					else if (MinGapLimit < KeyManager.AbsoluteMinGapLimit)
					{
						ValidationMessage = $"Min Gap Limit cannot be smaller than {KeyManager.AbsoluteMinGapLimit}.";
					}
					else if (MinGapLimit > 1_000_000)
					{
						ValidationMessage = $"Min Gap Limit cannot be larger than {1_000_000}.";
					}
					else if (!KeyPath.TryParse(AccountKeyPath, out KeyPath keyPath))
					{
						ValidationMessage = "The account key path is not a valid derivation path.";
					}
					else
					{
						try
						{
							var mnemonic = new Mnemonic(MnemonicWords);
							KeyManager.Recover(mnemonic, Password, walletFilePath, keyPath, MinGapLimit);

							owner.SelectLoadWallet();
						}
						catch (Exception ex)
						{
							ValidationMessage = ex.ToTypeMessageString();
							Logger.LogError(ex);
						}
					}
				});

			this.WhenAnyValue(x => x.MnemonicWords).Subscribe(UpdateSuggestions);

			_suggestions = new ObservableCollection<SuggestionViewModel>();
		}

		public ErrorDescriptors ValidatePassword() => PasswordHelper.ValidatePassword(Password);

		[ValidateMethod(nameof(ValidatePassword))]
		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string MnemonicWords
		{
			get => _mnemonicWords;
			set => this.RaiseAndSetIfChanged(ref _mnemonicWords, value);
		}

		public ObservableCollection<SuggestionViewModel> Suggestions
		{
			get => _suggestions;
			set => this.RaiseAndSetIfChanged(ref _suggestions, value);
		}

		public string WalletName
		{
			get => _walletName;
			set => this.RaiseAndSetIfChanged(ref _walletName, value);
		}

		public string ValidationMessage
		{
			get => _validationMessage;
			set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
		}

		public int CaretIndex
		{
			get => _caretIndex;
			set => this.RaiseAndSetIfChanged(ref _caretIndex, value);
		}

		public bool ShowAdvancedOptions
		{
			get => _showAdvancedOptions;
			set => this.RaiseAndSetIfChanged(ref _showAdvancedOptions, value);
		}

		public string AccountKeyPath
		{
			get => _accountKeyPath;
			set => this.RaiseAndSetIfChanged(ref _accountKeyPath, value);
		}

		public int MinGapLimit
		{
			get => _minGapLimit;
			set => this.RaiseAndSetIfChanged(ref _minGapLimit, value);
		}

		public ReactiveCommand<Unit, Unit> RecoverCommand { get; }

		public void OnTermsClicked()
		{
			IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectTermsAndConditions();
		}

		public void OnPrivacyClicked()
		{
			IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectPrivacyPolicy();
		}

		public void OnLegalClicked()
		{
			IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectLegalIssues();
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = null;
			MnemonicWords = "";

			WalletName = Global.GetNextWalletName();

			ValidationMessage = null;
			ShowAdvancedOptions = false;
			AccountKeyPath = $"m/{KeyManager.DefaultAccountKeyPath}";
			MinGapLimit = KeyManager.AbsoluteMinGapLimit * 3;
		}

		private void UpdateSuggestions(string words)
		{
			if (string.IsNullOrWhiteSpace(words))
			{
				Suggestions?.Clear();
				return;
			}

			string[] enteredWordList = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var lastWord = enteredWordList.LastOrDefault().Replace("\t", "");
			if (lastWord.Length < 1)
			{
				Suggestions.Clear();
				return;
			}

			var suggestedWords = EnglishWords.Where(w => w.StartsWith(lastWord)).Except(enteredWordList).Take(7);

			Suggestions.Clear();
			foreach (var suggestion in suggestedWords)
			{
				Suggestions.Add(new SuggestionViewModel(suggestion, OnAddWord));
			}
		}

		public void OnAddWord(string word)
		{
			string[] words = MnemonicWords.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (words.Length == 0)
			{
				MnemonicWords = word + " ";
			}
			else
			{
				words[words.Length - 1] = word;
				MnemonicWords = string.Join(' ', words) + " ";
			}

			CaretIndex = MnemonicWords.Length;

			Suggestions.Clear();
		}

		private static IEnumerable<string> EnglishWords { get; } = Wordlist.English.GetWords();
	}
}
