using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Confirm Multi-share")]
public partial class ConfirmMultiShareViewModel : RoutableViewModel
{
	private const int WordsToConfirmPerPage = 3;

	private static readonly Dictionary<int, byte> WordsPerPageMap = new()
	{
		[12] = 12,
		[18] = 9,
		[20] = 10,
		[24] = 12,
		[33] = 11,
	};

	private static readonly Dictionary<int, byte> TotalPagesMap = new()
	{
		[12] = 1,
		[18] = 2,
		[20] = 2,
		[24] = 2,
		[33] = 3,
	};

	private readonly WalletCreationOptions.AddNewWallet _options;
	private readonly Dictionary<int, List<RecoveryWordViewModel>> _wordsDictionary;
	private readonly List<RecoveryWordViewModel> _words;
	private readonly HashSet<int> _wordIndexesToConfirm;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _currentShare;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _totalShares;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _currentSharePage;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _totalCurrenSharePages;

	[AutoNotify] private bool _isSkipEnabled;
	[AutoNotify] private RecoveryWordViewModel _currentWord;
	[AutoNotify] private List<RecoveryWordViewModel> _availableWords;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _allWordsConfirmed;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private string _caption = "";

	private ConfirmMultiShareViewModel(WalletCreationOptions.AddNewWallet options, Dictionary<int, List<RecoveryWordViewModel>> wordsDictionary)
	{
		var multiShareBackup = options.SelectedWalletBackup as MultiShareBackup;

		ArgumentNullException.ThrowIfNull(multiShareBackup);

		_currentShare = multiShareBackup.CurrentShare;
		_totalShares = multiShareBackup.Settings.Shares;

		var words = wordsDictionary[_currentShare - 1];

		_wordsDictionary = wordsDictionary;

		_options = options;
		_availableWords = new List<RecoveryWordViewModel>();

		// Paginate recovery words.
		var wordsPerPage = WordsPerPageMap[words.Count];
		_currentSharePage = multiShareBackup.CurrentSharePage;
		_totalCurrenSharePages = TotalPagesMap[words.Count];
		var wordsToSkip = wordsPerPage * (_currentSharePage - 1);

		_words = words
			.Skip(wordsToSkip)
			.Take(wordsPerPage)
			.OrderBy(x => x.Index)
			.ToList();

		_wordIndexesToConfirm = GetRandomIndexes(
			array: _words.Select(x => x.Index).ToArray(),
			count: WordsToConfirmPerPage);

		ResetWords();

		ConfirmNotRequiredWords(_words);

		if (_words.FirstOrDefault(x => !x.IsConfirmed) is { } nextWord)
		{
			_currentWord = nextWord;
		}
		else
		{
			_currentWord = _words.Last();
		}
	}

	public ObservableCollectionExtended<RecoveryWordViewModel> ConfirmationWords { get; } = new();

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		ResetWords();

		ConfirmationWords.Clear();

		var confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();

		confirmationWordsSourceList
			.DisposeWith(disposables)
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(ConfirmationWords)
			.OnItemAdded(x => x.Reset())
			.Subscribe()
			.DisposeWith(disposables);

		EnableBack = true;

		CancelCommand = ReactiveCommand.Create(OnCancel);

		var nextCommandCanExecute =
			confirmationWordsSourceList
				.Connect()
				.WhenValueChanged(x => x.IsConfirmed)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(_ => confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(), nextCommandCanExecute);

		nextCommandCanExecute.Do(x => AllWordsConfirmed = x)
			.Subscribe()
			.DisposeWith(disposables);

		this.WhenAnyValue(
				x => x.CurrentShare,
				x => x.TotalShares,
				x => x.CurrentSharePage,
				x => x.TotalCurrenSharePages,
				x => x.CurrentWord,
				x => x.AllWordsConfirmed)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				var currentWordMessage = AllWordsConfirmed ? "Recovery words confirmed." : $"Click the recovery word #{CurrentWord.Index}";

				Caption = $"Share #{CurrentShare} of {TotalShares} - Page #{CurrentSharePage} of {TotalCurrenSharePages}. {currentWordMessage}";
			})
			.DisposeWith(disposables);

		SetSkip();

		confirmationWordsSourceList.AddRange(_words);

		ConfirmNotRequiredWords(_words);

		AvailableWords = confirmationWordsSourceList.Items
			.Select(x => new RecoveryWordViewModel(x.Index, x.Word))
			.OrderBy(x => x.Word)
			.ToList();

		var availableWordsSourceList = new SourceList<RecoveryWordViewModel>()
			.DisposeWith(disposables);

		availableWordsSourceList
			.Connect()
			.WhenPropertyChanged(x => x.IsSelected)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(x => OnWordSelectionChanged(x.Sender))
			.DisposeWith(disposables);

		availableWordsSourceList.AddRange(AvailableWords);

		ConfirmNotRequiredWords(AvailableWords);

		SetNextWord();

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}

	private void ConfirmNotRequiredWords(IEnumerable<RecoveryWordViewModel> words)
	{
		foreach (var word in words)
		{
			if (!_wordIndexesToConfirm.Contains(word.Index))
			{
				word.SelectedWord = word.Word;
				word.IsConfirmed = true;
				word.IsEnabled = false;
			}
			else
			{
				word.SelectedWord = null;
				word.IsConfirmed = false;
				word.IsEnabled = true;
			}
		}
	}

	private static HashSet<int> GetRandomIndexes(int[] array, int count)
	{
		if (count > array.Length)
		{
			throw new ArgumentException("Count cannot be greater than array length.");
		}

		for (var i = array.Length - 1; i > 0; i--)
		{
			var j = RandomNumberGenerator.GetInt32(0, i + 1);
			(array[i], array[j]) = (array[j], array[i]);
		}

		return array.Take(count).ToHashSet();
	}

	private void ResetWords()
	{
		foreach (var word in _words)
		{
			word.Reset();
		}
	}

	private void SetNextWord()
	{
		if (ConfirmationWords.FirstOrDefault(x => !x.IsConfirmed) is { } nextWord)
		{
			CurrentWord = nextWord;
		}
		else
		{
			CurrentWord = ConfirmationWords.Last();
		}

		EnableAvailableWords(true);
	}

	private void OnWordSelectionChanged(RecoveryWordViewModel selectedWord)
	{
		if (selectedWord.IsSelected)
		{
			CurrentWord.SelectedWord = selectedWord.Word;
		}
		else
		{
			if (!CurrentWord.IsConfirmed)
			{
				CurrentWord.SelectedWord = null;
			}
		}

		if (CurrentWord.IsConfirmed)
		{
			selectedWord.IsConfirmed = true;
			SetNextWord();
		}
		else if (!selectedWord.IsSelected)
		{
			EnableAvailableWords(true);
		}
		else
		{
			EnableAvailableWords(false);
			selectedWord.IsEnabled = true;
		}
	}

	private void EnableAvailableWords(bool enable)
	{
		foreach (var w in AvailableWords)
		{
			w.IsEnabled = enable;
		}
	}

	private async Task OnNextAsync(bool skip = false)
	{
		var options = _options;

		if (options.SelectedWalletBackup is not MultiShareBackup multiShareBackup)
		{
			throw new ArgumentOutOfRangeException(nameof(options));
		}

		if ((_currentShare >= multiShareBackup.Settings.Shares && _currentSharePage >= _totalCurrenSharePages)
		    || skip)
		{
			var dialogCaption = "Store your passphrase safely, it cannot be reset if lost.\n" +
			                    "It's needed to open and to recover your wallet.\n" +
			                    "It's a recovery words extension for more security.";
			var password = await Navigate().To()
				.CreatePasswordDialog("Add Passphrase", dialogCaption, enableEmpty: true).GetResultAsync();

			if (password is null)
			{
				return;
			}

			options = options with
			{
				SelectedWalletBackup = multiShareBackup with
				{
					Password = password
				}
			};

			var walletSettings = await UiContext.WalletRepository.NewWalletAsync(options);
			Navigate().To().AddedWalletPage(walletSettings, options);
		}
		else
		{
			var isLastSharePage = _currentSharePage == _totalCurrenSharePages;
			var nextShare = (byte)(isLastSharePage ? _currentShare + 1 : _currentShare);
			var nextSharePage = (byte)(isLastSharePage ? 1 : _currentSharePage + 1);

			options = options with
			{
				SelectedWalletBackup = multiShareBackup with
				{
					CurrentShare = nextShare,
					CurrentSharePage = nextSharePage
				}
			};

			Navigate().To().ConfirmMultiShare(options, _wordsDictionary);
		}
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	private void SetSkip()
	{
#if RELEASE
		IsSkipEnabled = Services.WalletManager.Network != NBitcoin.Network.Main || System.Diagnostics.Debugger.IsAttached;
#else
		IsSkipEnabled = true;
#endif

		if (IsSkipEnabled)
		{
			SkipCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(true));
		}
	}
}
