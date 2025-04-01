using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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

	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _currentShare;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _totalShares;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _currentSharePage;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private byte _totalCurrenSharePages;

	[AutoNotify] private bool _isSkipEnabled;
	[AutoNotify] private RecoveryWordViewModel _currentWord;
	[AutoNotify] private List<RecoveryWordViewModel> _availableWords;

	private ConfirmMultiShareViewModel(WalletCreationOptions.AddNewWallet options, Dictionary<int, List<RecoveryWordViewModel>> wordsDictionary)
	{
		var multiShareBackup = options.SelectedWalletBackup as MultiShareBackup;

		ArgumentNullException.ThrowIfNull(multiShareBackup);
		ArgumentNullException.ThrowIfNull(multiShareBackup.Shares);
		ArgumentNullException.ThrowIfNull(multiShareBackup.Settings);

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
		_currentWord = words.First();
	}

	public ObservableCollectionExtended<RecoveryWordViewModel> ConfirmationWords { get; } = new();

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

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
				.Select(_ => confirmationWordsSourceList.Items.All(x => x.IsConfirmed));

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(), nextCommandCanExecute);

		SetSkip();

		confirmationWordsSourceList.AddRange(_words);

		AvailableWords = confirmationWordsSourceList.Items
			.Select(x => new RecoveryWordViewModel(x.Index, x.Word))
			.OrderBy(x => x.Word)
			.ToList();

		var availableWordsSourceList = new SourceList<RecoveryWordViewModel>()
			.DisposeWith(disposables);

		availableWordsSourceList
			.Connect()
			.WhenPropertyChanged(x => x.IsSelected)
			.Subscribe(x => OnWordSelectionChanged(x.Sender))
			.DisposeWith(disposables);

		availableWordsSourceList.AddRange(AvailableWords);

		SetNextWord();

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}

	private void SetNextWord()
	{
		if (ConfirmationWords.FirstOrDefault(x => !x.IsConfirmed) is { } nextWord)
		{
			CurrentWord = nextWord;
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
			CurrentWord.SelectedWord = null;
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
			options = options with
			{
				SelectedWalletBackup = multiShareBackup with
				{
					CurrentShare = _currentSharePage == _totalCurrenSharePages ? ++_currentShare : _currentShare,
					CurrentSharePage = (byte)(_currentSharePage == _totalCurrenSharePages ? 1 : ++_currentSharePage)
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
