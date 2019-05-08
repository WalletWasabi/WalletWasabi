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
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

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

		public RecoverWalletViewModel(WalletManagerViewModel owner) : base("Recover Wallet")
		{
			MnemonicWords = "";

			RecoverCommand = ReactiveCommand.Create(() =>
			{
				WalletName = Guard.Correct(WalletName);
				MnemonicWords = Guard.Correct(MnemonicWords);
				Password = Guard.Correct(Password); // Don't let whitespaces to the beginning and to the end.

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
				else if (!TryParseKeyPath(AccountKeyPath))
				{
					ValidationMessage = "The account key path is not a valid derivation path.";
				}
				else
				{
					KeyPath keyPath = KeyPath.Parse(AccountKeyPath);
					try
					{
						var mnemonic = new Mnemonic(MnemonicWords);
						KeyManager.Recover(mnemonic, Password, walletFilePath, keyPath, MinGapLimit);

						owner.SelectLoadWallet();
					}
					catch (Exception ex)
					{
						ValidationMessage = ex.ToTypeMessageString();
						Logger.LogError<LoadWalletViewModel>(ex);
					}
				}
			});

			this.WhenAnyValue(x => x.MnemonicWords).Subscribe(x => UpdateSuggestions(x));
			this.WhenAnyValue(x => x.Password).Subscribe(x =>
			{
				try
				{
					if (x.NotNullAndNotEmpty())
					{
						char lastChar = x.Last();
						if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
						{
							Password = x.TrimEnd('\r', '\n');
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogTrace(ex);
				}
			});

			this.WhenAnyValue(x => x.CaretIndex).Subscribe(_ =>
			{
				if (CaretIndex != MnemonicWords.Length)
				{
					CaretIndex = MnemonicWords.Length;
				}
			});

			_suggestions = new ObservableCollection<SuggestionViewModel>();
		}

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
			MnemonicWords = "";
			WalletName = Utils.GetNextWalletName();
			ValidationMessage = null;
			ShowAdvancedOptions = false;
			AccountKeyPath = $"m/{KeyManager.DefaultAccountKeyPath}";
			MinGapLimit = KeyManager.AbsoluteMinGapLimit;
		}

		private void UpdateSuggestions(string words)
		{
			if (string.IsNullOrWhiteSpace(words))
			{
				Suggestions?.Clear();
				return;
			}

			string[] enteredWordList = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var lastWorld = enteredWordList.LastOrDefault().Replace("\t", "");

			if (lastWorld.Length < 1)
			{
				Suggestions.Clear();
				return;
			}

			var suggestedWords = EnglishWords.Where(w => w.StartsWith(lastWorld)).Except(enteredWordList).Take(7);

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

		private bool TryParseKeyPath(string keyPath)
		{
			try
			{
				KeyPath.Parse(keyPath);
				return true;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		private static IEnumerable<string> EnglishWords { get; } = Wordlist.English.GetWords();
	}
}
