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

namespace WalletWasabi.Fluent.ViewModels.AddWallet.Create;

[NavigationMetaData(Title = "Confirm Recovery Words")]
public partial class ConfirmRecoveryWordsViewModel : RoutableViewModel
{
	private const int NumberOfOptions = 3;
	private readonly List<RecoveryWordViewModel> _words;
	private readonly Mnemonic _mnemonic;
	private readonly string _walletName;

	[AutoNotify] private bool _isSkipEnabled;
	[AutoNotify] private string? _selectedWord;
	[AutoNotify] private RecoveryWordViewModel? _currentWord;

	public ConfirmRecoveryWordsViewModel(List<RecoveryWordViewModel> words, Mnemonic mnemonic, string walletName)
	{
		_words = words;
		_mnemonic = mnemonic;
		_walletName = walletName;
	}

	public ObservableCollectionExtended<RecoveryWordViewModel> ConfirmationWords { get; } = new();

	public ObservableCollectionExtended<string> AvailableWords { get; } = new();

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		if (isInHistory)
		{
			Navigate().Back();
			return;
		}

		base.OnNavigatedTo(isInHistory, disposables);

		var confirmationWordsSourceList = new SourceList<RecoveryWordViewModel>();

		confirmationWordsSourceList
			.DisposeWith(disposables)
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Sort(SortExpressionComparer<RecoveryWordViewModel>.Ascending(x => x.Index))
			.Bind(ConfirmationWords)
			.OnItemAdded(x => x.Reset())
			.WhenValueChanged(x => x.IsConfirmed)
			.Select(_ => confirmationWordsSourceList.Items.All(x => x.IsConfirmed))
			.Where(x => x)
			.DoAsync(_ => OnNextAsync())
			.Subscribe()
			.DisposeWith(disposables);

		EnableBack = true;

		CancelCommand = ReactiveCommand.Create(OnCancel);

		SetSkip();

		confirmationWordsSourceList.AddRange(_words);

		SetNextWord();

		this.WhenAnyValue(x => x.SelectedWord)
			.Skip(1)
			.DoAsync(_ => OnWordSelectedAsync())
			.Subscribe()
			.DisposeWith(disposables);

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

		AvailableWords.Clear();

		if (CurrentWord is { })
		{
			var words =
			_mnemonic.WordList
					 .GetWords()
					 .OrderBy(_ => Random.Shared.Next())
					 .Take(NumberOfOptions - 1)
					 .Concat(new[] { CurrentWord.Word })
					 .OrderBy(_ => Random.Shared.Next())
					 .ToList();

			AvailableWords.AddRange(words);
		}
	}

	private async Task OnWordSelectedAsync()
	{
		if (CurrentWord is null)
		{
			return;
		}

		CurrentWord.SelectedWord = SelectedWord;

		if (CurrentWord.IsConfirmed)
		{
			SetNextWord();
		}
		else
		{
			await NavigateDialogAsync(new ConfirmRecoveryWordsTryAgainViewModel(), NavigationTarget.CompactDialogScreen);

			Navigate().Back();
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
		else
		{
			Navigate().Back();
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
