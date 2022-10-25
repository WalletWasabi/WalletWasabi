using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
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
	private readonly IEnumerable<SmartCoin> _predefinedCoins;
	private readonly WalletViewModel _walletViewModel;
	private readonly TransactionInfo _transactionInfo;

	[AutoNotify] private string _searchFilter = "";
	[AutoNotify] private ReactiveCommand<Unit, Unit> _clearCoinSelectionCommand = ReactiveCommand.Create(() => { });
	[AutoNotify] private IObservable<bool> _enoughSelected = Observable.Return(false);
	[AutoNotify] private IObservable<bool> _isSelectionBadlyChosen = Observable.Return(false);
	[AutoNotify] private LabelBasedCoinSelectionViewModel? _labelBasedSelection;
	[AutoNotify] private IObservable<Money> _remainingAmount = Observable.Return(Money.Zero);
	[AutoNotify] private ReactiveCommand<Unit, Unit> _selectAllCoinsCommand = ReactiveCommand.Create(() => { });
	[AutoNotify] private ReactiveCommand<Unit, Unit> _selectPrivateCoinsCommand = ReactiveCommand.Create(() => { });
	[AutoNotify] private IObservable<Money> _selectedAmount = Observable.Return(Money.Zero);
	[AutoNotify] private IObservable<int> _selectedCount = Observable.Return(0);
	[AutoNotify] private IObservable<Money> _requiredAmount = Observable.Return(Money.Zero);
	[AutoNotify] private ReactiveCommand<Unit, Unit> _selectPredefinedCoinsCommand = ReactiveCommand.Create(() => { });
	[AutoNotify] private IObservable<string> _summaryText = Observable.Return("");

	public SelectCoinsDialogViewModel(WalletViewModel walletViewModel, TransactionInfo transactionInfo, IEnumerable<SmartCoin> predefinedCoins)
	{
		_walletViewModel = walletViewModel;
		_transactionInfo = transactionInfo;
		_balanceChanged = walletViewModel.UiTriggers.BalanceUpdateTrigger;
		_predefinedCoins = predefinedCoins;
		
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;
		NextCommand = ReactiveCommand.Create(() => new List<ICoin>());
	}

	private new ReactiveCommand<Unit, List<ICoin>> NextCommand { get; set; }

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Objects use DisposeWith")]
	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var coinChanges = _balanceChanged
			.StartWith(Unit.Default)
			.SelectMany(_ => GetCoins())
			.ToObservableChangeSet(x => x.OutPoint)
			.TransformWithInlineUpdate(coin => new SelectableCoin(coin), (selectableCoin, smartCoin) => selectableCoin.Coin = smartCoin)
			.Replay();

		coinChanges
			.Bind(out var coinCollection)
			.Subscribe()
			.DisposeWith(disposables);

		var allCoins = coinChanges
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Throttle(TimeSpan.FromMilliseconds(150), RxApp.MainThreadScheduler);

		var selectedCoins = allCoins
			.Select(items => items.Where(coin => coin.IsSelected).Select(x => x.Coin).ToList());

		SelectedAmount = selectedCoins.Select(coins => new Money(coins.Sum(x => x.Amount)));

		var requiredAmount = selectedCoins.Select(GetRequiredAmount)
			.Publish()
			.RefCount();

		RequiredAmount = requiredAmount;

		RemainingAmount = SelectedAmount.CombineLatest(RequiredAmount, (selected, remaining) => remaining - selected);

		EnoughSelected = RemainingAmount.Select(remaining => remaining <= Money.Zero);

		NextCommand = ReactiveCommand.CreateFromObservable(() => selectedCoins, EnoughSelected);
		NextCommand.Subscribe(CloseAndReturnCoins);

		SelectPredefinedCoinsCommand = ReactiveCommand.Create(() => coinCollection.ToList().ForEach(x => x.IsSelected = _predefinedCoins.Any(coin => x.Coin.OutPoint == coin.Outpoint)));

		SelectAllCoinsCommand = ReactiveCommand.Create(() => coinCollection.ToList().ForEach(x => x.IsSelected = true));

		ClearCoinSelectionCommand = ReactiveCommand.Create(() => coinCollection.ToList().ForEach(x => x.IsSelected = false));

		var areTherePrivateCoins = allCoins.Select(x => x.Any(c => c.PrivacyLevel == PrivacyLevel.Private));

		SelectPrivateCoinsCommand = ReactiveCommand.Create(() => coinCollection.ToList().ForEach(coinViewModel => coinViewModel.IsSelected = coinViewModel.PrivacyLevel == PrivacyLevel.Private), areTherePrivateCoins);

		var commands = new[]
		{
			new CommandViewModel("All", SelectAllCoinsCommand),
			new CommandViewModel("None", ClearCoinSelectionCommand),
			new CommandViewModel("Private coins", SelectPrivateCoinsCommand),
			new CommandViewModel("Smart", SelectPredefinedCoinsCommand)
		};

		var filterChanged = this
			.WhenAnyValue(x => x.SearchFilter)
			.Throttle(TimeSpan.FromMilliseconds(100), RxApp.MainThreadScheduler);

		LabelBasedSelection = new LabelBasedCoinSelectionViewModel(coinChanges, commands, filterChanged)
			.DisposeWith(disposables);

		coinChanges.Connect();

		SelectPredefinedCoinsCommand.Execute()
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private Money GetRequiredAmount(List<ICoin> coins)
	{
		TransactionHelpers.TryBuildTransactionWithoutPrevTx(_walletViewModel.Wallet.KeyManager, _transactionInfo, _walletViewModel.Wallet.Coins, GetAssociatedSmartCoins(coins), _walletViewModel.Wallet.Kitchen.SaltSoup(), out var minimumAmount);
		return minimumAmount;
	}

	private void CloseAndReturnCoins(IEnumerable<ICoin> coins)
	{
		var smartCoins = GetAssociatedSmartCoins(coins);
		Close(DialogResultKind.Normal, smartCoins);
	}

	private IEnumerable<SmartCoin> GetAssociatedSmartCoins(IEnumerable<ICoin> coins)
	{
		return coins.Join(_walletViewModel.Wallet.Coins, x => x.OutPoint, x => x.Outpoint, (_, smartCoin) => smartCoin);
	}

	private IEnumerable<ICoin> GetCoins()
	{
		return _walletViewModel.Wallet.Coins.Select(coin => new SmartCoinAdapter(coin, _walletViewModel.Wallet.AnonScoreTarget));
	}
}
