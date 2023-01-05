using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Views.AddWallet.Create;

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Confirm Recovery Words")]
public partial class ConfirmRecoveryWordsViewModel : RoutableViewModel
{
	private readonly List<RecoveryWordViewModel> _words;
	private readonly Mnemonic _mnemonic;
	private readonly string _walletName;

	[AutoNotify] private bool _isSkipEnabled;
	[AutoNotify] private RecoveryWordViewModel? _currentWord;
	[AutoNotify] private List<RecoveryWordViewModel> _availableWords;

	public ConfirmRecoveryWordsViewModel(List<RecoveryWordViewModel> words, Mnemonic mnemonic, string walletName)
	{
		_availableWords = new List<RecoveryWordViewModel>();
		_words = words;
		_mnemonic = mnemonic;
		_walletName = walletName;
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
			.Sort(SortExpressionComparer<RecoveryWordViewModel>.Ascending(x => x.Index))
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

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync, nextCommandCanExecute);

		SetSkip();

		confirmationWordsSourceList.AddRange(_words);

		AvailableWords =
			confirmationWordsSourceList.Items
									   .Select(x => new RecoveryWordViewModel(x.Index, x.Word))
									   .OrderBy(_ => Random.Shared.Next())
									   .ToList();

		var availableWordsSourceList = new SourceList<RecoveryWordViewModel>();

		availableWordsSourceList
			.DisposeWith(disposables)
			.Connect()
			.WhenPropertyChanged(x => x.IsSelected)
			.Subscribe(x => OnWordSelected(x.Sender));

		availableWordsSourceList.AddRange(AvailableWords);

		SetNextWord();

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}

	private void SetNextWord()
	{
		CurrentWord =
			CurrentWord?.Index switch
			{
				null => ConfirmationWords.First(),
				int i when i >= 1 && i < ConfirmationWords.Count => ConfirmationWords[i],
				_ => null
			};

		if (CurrentWord is { })
		{
			EnableAvailableWords(true);
		}
	}

	private void OnWordSelected(RecoveryWordViewModel selectedWord)
	{
		if (selectedWord.IsConfirmed)
		{
			selectedWord.IsSelected = true;
			return;
		}

		if (CurrentWord is null)
		{
			return;
		}

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

	private async Task OnNextAsync()
	{
		var dialogResult = await NavigateDialogAsync(
			new CreatePasswordDialogViewModel("Add Password", "This is needed to open and to recover your wallet. Store it safely, it cannot be changed.", enableEmpty: true),
			NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is { } password)
		{
			IsBusy = true;

			var (km, mnemonic) = await Task.Run(
				() =>
				{
					var walletGenerator = new WalletGenerator(
						Services.WalletManager.WalletDirectories.WalletsDir,
						Services.WalletManager.Network)
					{
						TipHeight = Services.BitcoinStore.SmartHeaderChain.TipHeight
					};
					return walletGenerator.GenerateWallet(_walletName, password, _mnemonic);
				});
			IsBusy = false;
			await NavigateDialogAsync(new CoinJoinProfilesViewModel(km, true), NavigationTarget.DialogScreen);
		}
	}

	private void OnCancel()
	{
		Navigate().Clear();
	}

	private void SetSkip()
	{
#if RELEASE
		IsSkipEnabled = Services.WalletManager.Network != Network.Main || System.Diagnostics.Debugger.IsAttached;
#else
		IsSkipEnabled = true;
#endif

		if (IsSkipEnabled)
		{
			SkipCommand = ReactiveCommand.CreateFromTask(OnNextAsync);
		}
	}
}
