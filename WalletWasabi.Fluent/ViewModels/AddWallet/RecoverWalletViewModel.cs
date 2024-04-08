using System.Collections.Generic;
using System.Collections.ObjectModel;
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
	[AutoNotify] private IEnumerable<string>? _suggestions;
	[AutoNotify] private Mnemonic? _currentMnemonics;
	[AutoNotify] private bool _isMnemonicsValid;
	[AutoNotify] private string? _confirmPassphrase;
	[AutoNotify] private bool _focusConfirmPassphrase;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _allWordsConfirmed;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _passphraseConfirmed;

	private RecoverWalletViewModel(WalletCreationOptions.RecoverWallet options)
	{
		var words = Enumerable
			.Range(1, 12)
			.Select(x => new RecoverWordViewModel(x, ""));

		_words = words.OrderBy(x => x.Index).ToList();
		_currentWord = _words.First();
		_currentWord.IsSelected = true;

		Suggestions = new Mnemonic(Wordlist.English, WordCount.Twelve).WordList.GetWords();

		Mnemonics.ToObservableChangeSet().ToCollection()
			.Select(x => x.Count is 12 or 15 or 18 or 21 or 24 ? new Mnemonic(GetTagsAsConcatString().ToLowerInvariant()) : null)
			.Subscribe(x =>
			{
				CurrentMnemonics = x;
				IsMnemonicsValid = x is { IsValidChecksum: true };
				this.RaisePropertyChanged(nameof(Mnemonics));
			});

		this.ValidateProperty(x => x.Mnemonics, ValidateMnemonics);

		EnableBack = true;

		var nextCanExecute = this.WhenAnyValue(x => x.IsMnemonicsValid);

		NextCommand = ReactiveCommand.CreateFromTask(
			async () => await OnNextAsync(options),
			canExecute: nextCanExecute);

		AdvancedRecoveryOptionsDialogCommand = ReactiveCommand.CreateFromTask(OnAdvancedRecoveryOptionsDialogAsync);

		NextWordCommand = ReactiveCommand.Create(NextWord);

		PreviousWordCommand = ReactiveCommand.Create(PreviousWord);

		SelectWordCommand = ReactiveCommand.Create<RecoverWordViewModel>(SelectWord);
	}

	public ICommand NextWordCommand { get; }

	public ICommand PreviousWordCommand { get; }

	public ICommand SelectWordCommand { get; }

	public ObservableCollectionExtended<RecoverWordViewModel> ConfirmationWords { get; } = new();

	public ICommand AdvancedRecoveryOptionsDialogCommand { get; }

	private int MinGapLimit { get; set; } = 114;

	public ObservableCollection<string> Mnemonics { get; } = new();

	private void NextWord()
	{
		var currentIndex = _words.IndexOf(_currentWord);
		if (currentIndex >= _words.Count - 1)
		{
			_currentWord.IsSelected = false;
			FocusConfirmPassphrase = true;
			FocusConfirmPassphrase = false;
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

	private async Task OnNextAsync(WalletCreationOptions.RecoverWallet options)
	{
		var (walletName, _, _, _) = options;
		ArgumentException.ThrowIfNullOrEmpty(walletName);

		var password = ConfirmPassphrase;
		if (password is not { } || CurrentMnemonics is not { IsValidChecksum: true } currentMnemonics)
		{
			return;
		}

		IsBusy = true;

		try
		{
			options = options with { Passphrase = password, Mnemonic = currentMnemonics, MinGapLimit = MinGapLimit };
			var wallet = await UiContext.WalletRepository.NewWalletAsync(options);
			await Navigate().To().CoinJoinProfiles(wallet, options).GetResultAsync();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "Wasabi was unable to recover the wallet.");
		}

		IsBusy = false;
	}

	private async Task OnAdvancedRecoveryOptionsDialogAsync()
	{
		var result = await Navigate().To().AdvancedRecoveryOptions(MinGapLimit).GetResultAsync();
		if (result is { } minGapLimit)
		{
			MinGapLimit = minGapLimit;
		}
	}

	private void ValidateMnemonics(IValidationErrors errors)
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

		if (!Mnemonics.Any())
		{
			return;
		}

		errors.Add(ErrorSeverity.Error, "Invalid set. Make sure you typed all your recovery words in the correct order.");
	}

	private string GetTagsAsConcatString()
	{
		return string.Join(' ', Mnemonics);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		ConfirmationWords.Clear();

		var confirmationWordsSourceList = new SourceList<RecoverWordViewModel>();

		confirmationWordsSourceList
			.DisposeWith(disposables)
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(ConfirmationWords)
			//.OnItemAdded(x => x.Reset())
			.Subscribe()
			.DisposeWith(disposables);

		confirmationWordsSourceList
			.Connect()
			.WhenValueChanged(x => x.IsConfirmed)
			.Subscribe(_ => AllWordsConfirmed = confirmationWordsSourceList.Items.All(x => x.IsConfirmed))
			.DisposeWith(disposables);

		confirmationWordsSourceList.AddRange(_words);

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: enableCancel, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
