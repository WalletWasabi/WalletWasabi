using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Suggestions;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Tabs.WalletManager.RecoverWallets
{
	internal class RecoverWalletViewModel : CategoryViewModel
	{
		private int _caretIndex;
		private string _password;
		private string _mnemonicWords;
		private string _walletName;
		private string _accountKeyPath;
		private string _minGapLimit;
		private ObservableCollection<SuggestionViewModel> _suggestions;

		public RecoverWalletViewModel(WalletManagerViewModel owner) : base("Recover Wallet")
		{
			Global = Locator.Current.GetService<Global>();
			WalletManager = Global.WalletManager;

			this.ValidateProperty(x => x.Password, ValidatePassword);
			this.ValidateProperty(x => x.MinGapLimit, ValidateMinGapLimit);
			this.ValidateProperty(x => x.AccountKeyPath, ValidateAccountKeyPath);

			MnemonicWords = "";

			var canExecute = Observable
				.Merge(Observable.FromEventPattern(this, nameof(ErrorsChanged)).Select(_ => Unit.Default))
				.Merge(this.WhenAnyValue(x => x.MnemonicWords).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(_ =>
				{
					var numberOfWords = MnemonicWords.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
					return !Validations.AnyErrors && (numberOfWords == 12 || numberOfWords == 15 || numberOfWords == 18 || numberOfWords == 21 || numberOfWords == 24);
				});

			RecoverCommand = ReactiveCommand.Create(() => RecoverWallet(owner), canExecute);

			this.WhenAnyValue(x => x.MnemonicWords).Subscribe(UpdateSuggestions);

			_suggestions = new ObservableCollection<SuggestionViewModel>();

			RecoverCommand.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private void RecoverWallet(WalletManagerViewModel owner)
		{
			WalletName = Guard.Correct(WalletName);
			MnemonicWords = Guard.Correct(MnemonicWords);
			Password = Guard.Correct(Password); // Do not let whitespaces to the beginning and to the end.

			string walletFilePath = WalletManager.WalletDirectories.GetWalletFilePaths(WalletName).walletFilePath;

			if (string.IsNullOrWhiteSpace(WalletName))
			{
				NotificationHelpers.Error("Invalid wallet name.");
			}
			else if (File.Exists(walletFilePath))
			{
				NotificationHelpers.Error("Wallet name is already taken.");
			}
			else if (string.IsNullOrWhiteSpace(MnemonicWords))
			{
				NotificationHelpers.Error("Recovery Words were not supplied.");
			}
			else
			{
				var minGapLimit = int.Parse(MinGapLimit);
				var keyPath = KeyPath.Parse(AccountKeyPath);

				try
				{
					var mnemonic = new Mnemonic(MnemonicWords);
					var km = KeyManager.Recover(mnemonic, Password, filePath: null, keyPath, minGapLimit);
					km.SetNetwork(Global.Network);
					km.SetFilePath(walletFilePath);
					WalletManager.AddWallet(km);

					NotificationHelpers.Success("Wallet was recovered.");

					owner.SelectLoadWallet(km);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				}
			}
		}

		private Global Global { get; }
		private Wallets.WalletManager WalletManager { get; }

		private void ValidatePassword(IValidationErrors errors) => PasswordHelper.ValidatePassword(errors, Password);

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

		public int CaretIndex
		{
			get => _caretIndex;
			set => this.RaiseAndSetIfChanged(ref _caretIndex, value);
		}

		public string AccountKeyPath
		{
			get => _accountKeyPath;
			set => this.RaiseAndSetIfChanged(ref _accountKeyPath, value);
		}

		public string MinGapLimit
		{
			get => _minGapLimit;
			set => this.RaiseAndSetIfChanged(ref _minGapLimit, value);
		}

		public ReactiveCommand<Unit, Unit> RecoverCommand { get; }

		private static IEnumerable<string> EnglishWords { get; } = Wordlist.English.GetWords();

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();

			Password = null;
			MnemonicWords = "";

			WalletName = WalletManager.WalletDirectories.GetNextWalletName();

			AccountKeyPath = $"m/{KeyManager.DefaultAccountKeyPath}";
			MinGapLimit = (KeyManager.AbsoluteMinGapLimit * 3).ToString();
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
				words[^1] = word;
				MnemonicWords = string.Join(' ', words) + " ";
			}

			CaretIndex = MnemonicWords.Length;

			Suggestions.Clear();
		}

		private void ValidateMinGapLimit(IValidationErrors errors)
		{
			if (!int.TryParse(MinGapLimit, out int minGapLimit) || minGapLimit < KeyManager.AbsoluteMinGapLimit || minGapLimit > KeyManager.MaxGapLimit)
			{
				errors.Add(ErrorSeverity.Error, $"Must be a number between {KeyManager.AbsoluteMinGapLimit} and {KeyManager.MaxGapLimit}.");
			}
		}

		private void ValidateAccountKeyPath(IValidationErrors errors)
		{
			if (string.IsNullOrWhiteSpace(AccountKeyPath))
			{
				errors.Add(ErrorSeverity.Error, "Path is not valid.");
			}
			else if (KeyPath.TryParse(AccountKeyPath, out var keyPath))
			{
				var accountKeyPath = keyPath.GetAccountKeyPath();
				if (keyPath.Length != accountKeyPath.Length || accountKeyPath.Length != KeyManager.DefaultAccountKeyPath.Length)
				{
					errors.Add(ErrorSeverity.Error, "Path is not a compatible account derivation path.");
				}
			}
			else
			{
				errors.Add(ErrorSeverity.Error, "Path is not a valid derivation path.");
			}
		}
	}
}
