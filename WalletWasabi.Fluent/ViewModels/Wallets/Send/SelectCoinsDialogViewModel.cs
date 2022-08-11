using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using DynamicData;
using NBitcoin;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
	Title = "Select Coins",
	Caption = "",
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SelectCoinsDialogViewModel : RoutableViewModel
{
	private readonly IObservable<Unit> _balanceChanged;
	private readonly WalletViewModel _walletViewModel;
	[AutoNotify] private IObservable<bool> _isAnySelected = Observable.Return(false);
	[AutoNotify] private IObservable<string> _selectedAmount = Observable.Return((0.0M).FormattedBtc());
	[AutoNotify] private CoinSelectionViewModel? _coinSelection;
	[AutoNotify] private LabelBasedCoinSelectionViewModel? _labelBasedSelection;
	
	public SelectCoinsDialogViewModel(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
	{
		_walletViewModel = walletViewModel;
		_balanceChanged = balanceChanged;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		var coins = CreateCoinsObservable(_balanceChanged);

		var coinChanges = coins
			.ToObservableChangeSet(c => c.HdPubKey.GetHashCode())
			.AsObservableCache()
			.Connect()
			.TransformWithInlineUpdate(x => new WalletCoinViewModel(x))
			.Replay(1)
			.RefCount();

		var selectedCoins = coinChanges
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Select(items => items.Where(t => t.IsSelected));
		
		IsAnySelected = selectedCoins
			.Any()
			.ObserveOn(RxApp.MainThreadScheduler);

		SelectedAmount = selectedCoins
			.Select(Sum)
			.ObserveOn(RxApp.MainThreadScheduler);

		CoinSelection = new CoinSelectionViewModel(coinChanges).DisposeWith(disposables);
		LabelBasedSelection = new LabelBasedCoinSelectionViewModel(coinChanges).DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}
	
	private static string Sum(IEnumerable<WalletCoinViewModel> coinViewModels)
	{
		return coinViewModels.Sum(coinViewModel => coinViewModel.Coin.Amount.ToDecimal(MoneyUnit.BTC)).FormattedBtc();
	}

	private IObservable<ICoinsView> CreateCoinsObservable(IObservable<Unit> balanceChanged)
	{
		var initial = Observable.Return(GetCoins());
		var coinJoinChanged = _walletViewModel.WhenAnyValue(model => model.IsCoinJoining);
		var coinsChanged = balanceChanged.ToSignal().Merge(coinJoinChanged.ToSignal());

		var coins = coinsChanged
			.Select(_ => GetCoins());

		var concat = initial.Concat(coins);
		return concat;
	}
	
	private ICoinsView GetCoins()
	{
		return _walletViewModel.Wallet.Coins;
	}
}
