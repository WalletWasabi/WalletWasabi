using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using ICoin = WalletWasabi.Fluent.ViewModels.CoinSelection.Core.ICoin;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

[NavigationMetaData(
	Title = "Select Coins",
	Caption = "",
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SelectCoinsDialogViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly IObservable<Unit> _balanceChanged;
	private readonly SmartLabel _transactionLabels;
	private readonly IEnumerable<SmartCoin> _usedCoins;
	private readonly WalletViewModel _walletViewModel;

	[AutoNotify] private string _searchFilter = "";
	[AutoNotify] private ReactiveCommand<Unit, Unit> _clearCoinSelectionCommand = ReactiveCommand.Create(() => { });
	[AutoNotify] private CoinBasedSelectionViewModel? _coinBasedSelection;
	[AutoNotify] private IObservable<bool> _enoughSelected = Observable.Return(false);
	[AutoNotify] private IObservable<bool> _isSelectionBadlyChosen = Observable.Return(false);
	[AutoNotify] private LabelBasedCoinSelectionViewModel? _labelBasedSelection;
	[AutoNotify] private IObservable<Money> _remainingAmount = Observable.Return(Money.Zero);
	[AutoNotify] private ReactiveCommand<Unit, Unit> _selectAllCoinsCommand = ReactiveCommand.Create(() => { });
	[AutoNotify] private ReactiveCommand<Unit, Unit> _selectAllPrivateCoinsCommand = ReactiveCommand.Create(() => { });
	[AutoNotify] private IObservable<Money> _selectedAmount = Observable.Return(Money.Zero);
	[AutoNotify] private IObservable<int> _selectedCount = Observable.Return(0);
	[AutoNotify] private ReactiveCommand<Unit, Unit> _selectPredefinedCoinsCommand = ReactiveCommand.Create(() => { });
	[AutoNotify] private IObservable<string> _summaryText = Observable.Return("");

	public SelectCoinsDialogViewModel(WalletViewModel walletViewModel, TransactionInfo transactionInfo, IObservable<Unit> balanceChanged, IEnumerable<SmartCoin> usedCoins)
	{
		_walletViewModel = walletViewModel;
		_balanceChanged = balanceChanged;
		_usedCoins = usedCoins;
		TargetAmount = transactionInfo.MinimumRequiredAmount == Money.Zero
			? transactionInfo.Amount
			: transactionInfo.MinimumRequiredAmount;

		_transactionLabels = transactionInfo.UserLabels;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;
		NextCommand = ReactiveCommand.Create(() => new List<ICoin>());
	}

	public Money TargetAmount { get; }

	private new ReactiveCommand<Unit, List<ICoin>> NextCommand { get; set; }

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Objects use DisposeWith")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var coinChanges = _balanceChanged
			.StartWith()
			.ObserveOn(RxApp.MainThreadScheduler)
			.SelectMany(_ => GetCoins())
			.ToObservableChangeSet(x => x.OutPoint)
			.TransformWithInlineUpdate(smartCoin => new SelectableCoin(smartCoin), (selectableCoin, smartCoin) => selectableCoin.Coin = smartCoin)
			.Replay();

		coinChanges.Connect().DisposeWith(disposables);

		coinChanges
			.Bind(out var coinCollection)
			.Subscribe()
			.DisposeWith(disposables);

		var allCoins = coinChanges
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Throttle(TimeSpan.FromMilliseconds(100), RxApp.MainThreadScheduler);

		var selectedCoins = allCoins
			.Select(items => items.Where(t => t.IsSelected).Select(x => x.Coin).ToList());
		var coinsSubject = new BehaviorSubject<List<ICoin>>(new List<ICoin>());

		selectedCoins.Subscribe(coinsSubject).DisposeWith(disposables);

		EnoughSelected = selectedCoins.Select(coins => coins.Sum(x => x.Amount) >= TargetAmount);

		IsSelectionBadlyChosen = allCoins.Select(coins => IsSelectionBadForPrivacy(coins, _transactionLabels)).ReplayLastActive();

		SelectedAmount = selectedCoins.Select(list => new Money(list.Sum(x => x.Amount)));

		RemainingAmount = SelectedAmount.Select(money => Money.Max(TargetAmount - money, Money.Zero));

		SelectedCount = selectedCoins.Select(models => models.Count);

		NextCommand = ReactiveCommand.CreateFromObservable(() => selectedCoins, EnoughSelected);
		NextCommand.Subscribe(CloseAndReturnCoins);

		SelectPredefinedCoinsCommand = ReactiveCommand.Create(() => coinCollection.ToList().ForEach(x => x.IsSelected = _usedCoins.Any(coin => x.Coin.OutPoint == coin.OutPoint)));

		SelectAllCoinsCommand = ReactiveCommand.Create(() => coinCollection.ToList().ForEach(x => x.IsSelected = true));

		ClearCoinSelectionCommand = ReactiveCommand.Create(() => coinCollection.ToList().ForEach(x => x.IsSelected = false));

		SelectAllPrivateCoinsCommand = ReactiveCommand.Create(() => coinCollection.ToList().ForEach(coinViewModel => coinViewModel.IsSelected = coinViewModel.PrivacyLevel == PrivacyLevel.Private));

		var commands = new[]
		{
			new CommandViewModel("All", SelectAllCoinsCommand),
			new CommandViewModel("None", ClearCoinSelectionCommand),
			new CommandViewModel("Private coins", SelectAllPrivateCoinsCommand),
			new CommandViewModel("Smart", SelectPredefinedCoinsCommand)
		};

		var filterChanged = this
			.WhenAnyValue(x => x.SearchFilter)
			.Throttle(TimeSpan.FromSeconds(0.1), RxApp.MainThreadScheduler);

		CoinBasedSelection = new CoinBasedSelectionViewModel(coinChanges, commands, filterChanged)
			.DisposeWith(disposables);

		LabelBasedSelection = new LabelBasedCoinSelectionViewModel(coinChanges, commands, filterChanged)
			.DisposeWith(disposables);

		SelectPredefinedCoinsCommand.Execute()
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private void CloseAndReturnCoins(IEnumerable<ICoin> coins)
	{
		var smartCoins = GetAssociatedSmartCoins(coins);
		Close(DialogResultKind.Normal, smartCoins);
	}

	private IEnumerable<SmartCoin> GetAssociatedSmartCoins(IEnumerable<ICoin> coins)
	{
		return coins.Join(_walletViewModel.Wallet.Coins, x => x.OutPoint, x => x.OutPoint, (_, smartCoin) => smartCoin);
	}

	private static Money Sum(IEnumerable<SelectableCoin> coinViewModels)
	{
		return coinViewModels.Sum(coinViewModel => coinViewModel.Amount);
	}

	private bool IsSelectionBadForPrivacy(IEnumerable<SelectableCoin> coins, SmartLabel transactionLabel)
	{
		var selectedCoins = coins.Where(x => x.IsSelected).ToList();

		if (selectedCoins.All(x => x.SmartLabel == transactionLabel || x.PrivacyLevel == PrivacyLevel.Private || x.PrivacyLevel == PrivacyLevel.SemiPrivate))
		{
			return false;
		}

		if (selectedCoins.Any(x => x.AnonymitySet == 1))
		{
			return true;
		}

		return false;
	}

	private IEnumerable<ICoin> GetCoins()
	{
		return _walletViewModel.Wallet.Coins.Select(coin => new SmartCoinAdapter(coin, _walletViewModel.Wallet.AnonScoreTarget));
	}
}
