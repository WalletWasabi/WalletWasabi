using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Recovery Words")]
public partial class RecoverWalletViewModel : RoutableViewModel
{
	private readonly List<RecoverWordViewModel> _words;
	[AutoNotify] private RecoverWordViewModel _currentWord;
	[AutoNotify] private Mnemonic? _currentMnemonics;
	[AutoNotify] private bool _isMnemonicsValid;
	[AutoNotify] private string? _passphrase;
	[AutoNotify] private bool _focusPassphrase;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _allWordsConfirmed;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _passphraseConfirmed;

	private RecoverWalletViewModel(WalletCreationOptions.RecoverWallet options)
	{
		var suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		var words = Enumerable
			.Range(1, 12)
			.Select(x => new RecoverWordViewModel(x, "", suggestions));

		_words = words.OrderBy(x => x.Index).ToList();
		_currentWord = _words.First();
		_currentWord.IsSelected = true;

		_passphrase = "";

		this.ValidateProperty(x => x.CurrentMnemonics, ValidateCurrentMnemonics);

		EnableBack = true;

		var nextCanExecute = this.WhenAnyValue(x => x.IsMnemonicsValid);

		NextCommand = ReactiveCommand.CreateFromTask(
			async () => await OnNextAsync(options),
			canExecute: nextCanExecute);

		AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(
			async () => await OnAdvancedRecoveryOptionsDialogAsync(options));

		var canExecuteNextWord = this.WhenAnyValue(x => x.CurrentWord.IsValid);

		NextWordCommand = ReactiveCommand.Create(NextWord, canExecuteNextWord);

		PreviousWordCommand = ReactiveCommand.Create(PreviousWord);

		SelectWordCommand = ReactiveCommand.Create<RecoverWordViewModel>(SelectWord);
	}

	public ObservableCollectionExtended<RecoverWordViewModel> RecoveryWords { get; } = new();

	public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

	public ICommand NextWordCommand { get; }

	public ICommand PreviousWordCommand { get; }

	public ICommand SelectWordCommand { get; }

	private int MinGapLimit { get; set; } = 114;

	private async Task OnNextAsync(WalletCreationOptions.RecoverWallet options)
	{
		var (walletName, _, _, _, _) = options;
		ArgumentException.ThrowIfNullOrEmpty(walletName);

		var passphrase = Passphrase;
		if (passphrase is not { } || CurrentMnemonics is not { IsValidChecksum: true } currentMnemonics)
		{
			return;
		}

		IsBusy = true;

		try
		{
			options = options with { Passphrase = passphrase, Mnemonic = currentMnemonics, MinGapLimit = MinGapLimit };
			Navigate().To().RecoverWalletSummary(options);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "Wasabi was unable to recover the wallet.");
		}

		IsBusy = false;
	}

	private async Task OnAdvancedRecoveryOptionsDialogAsync(WalletCreationOptions.RecoverWallet options)
	{
		var (walletName, _, _, _, _) = options;
		ArgumentException.ThrowIfNullOrEmpty(walletName);

		IsBusy = true;

		try
		{
			options = options with { Passphrase = Passphrase, Mnemonic = CurrentMnemonics, MinGapLimit = MinGapLimit };
			Navigate().To().RecoverWalletSummary(options);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "Wasabi was unable to recover the wallet.");
		}

		IsBusy = false;
	}

	private void NextWord()
	{
		var currentIndex = _words.IndexOf(_currentWord);
		if (currentIndex >= _words.Count - 1)
		{
			_currentWord.IsSelected = false;
			FocusPassphrase = true;
			FocusPassphrase = false;
		}
		else
		{
			_currentWord.IsSelected = false;
			_currentWord = _words[currentIndex + 1];
			_currentWord.IsSelected = true;
		}
	}

	private void PreviousWord()
	{
		var currentIndex = _words.IndexOf(_currentWord);
		if (currentIndex <= 0 || !_currentWord.IsSelected)
		{
			_currentWord.IsSelected = false;
			_currentWord = _words[^1];
			_currentWord.IsSelected = true;
		}
		else
		{
			_currentWord.IsSelected = false;
			_currentWord = _words[currentIndex - 1];
			_currentWord.IsSelected = true;
		}
	}

	private void SelectWord(RecoverWordViewModel word)
	{
		_currentWord.IsSelected = false;
		_currentWord = word;
		_currentWord.IsSelected = true;
	}

	private void ValidateCurrentMnemonics(IValidationErrors errors)
	{
		if (CurrentMnemonics is null)
		{
			ClearValidations();
			return;
		}

		if (IsMnemonicsValid)
		{
			return;
		}

		errors.Add(ErrorSeverity.Error, "Invalid set. Make sure you typed all your recovery words in the correct order.");
	}

	private string GetTagsAsConcatString()
	{
		return string.Join(' ', _words.Select(x => x.Word));
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		RecoveryWords.Clear();

		var recoveryWordsSourceList = new SourceList<RecoverWordViewModel>();

		recoveryWordsSourceList
			.DisposeWith(disposables)
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(RecoveryWords)
			.Subscribe()
			.DisposeWith(disposables);

		recoveryWordsSourceList.AddRange(_words);

		foreach (var word in _words)
		{
			word.WhenAnyValue(x => x.Word)
				.Subscribe(_ =>
				{
					var count = _words.Count(x => !string.IsNullOrEmpty(x.Word));
					try
					{
						var mnemonic = count is 12 or 15 or 18 or 21 or 24
							? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant())
							: null;
						CurrentMnemonics = mnemonic;
						IsMnemonicsValid = mnemonic is { IsValidChecksum: true };
					}
					catch (Exception)
					{
						CurrentMnemonics = null;
						IsMnemonicsValid = false;
					}
					this.RaisePropertyChanged(nameof(CurrentMnemonics));
				})
				.DisposeWith(disposables);;
		}

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
