using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using LinqKit;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

[NavigationMetaData(Title = "Select Coins", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinjoinCoinSelectionViewModel : DialogViewModelBase<Unit>
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private bool _hasSelection;
	[AutoNotify] private bool _isCoinjoining;

	public CoinjoinCoinSelectionViewModel(UiContext uiContext, IWalletModel wallet) : base(uiContext)
	{
		_wallet = wallet;

		var initialCoins = wallet.Coins.List.Items.Where(x => !x.IsExcludedFromCoinJoin);
		CoinList = new CoinListViewModel(uiContext, wallet.Coins, initialCoins.ToList(), allowCoinjoiningCoinSelection: false, ignorePrivacyMode: true);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = ReactiveCommand.Create(() => Close());
		ToggleSelectionCommand = ReactiveCommand.Create(() => SelectAll(!CoinList.Selection.Any()));
	}

	public CoinListViewModel CoinList { get; }

	public ICommand ToggleSelectionCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var coinItemChanges = CoinList.CoinItems.ToObservableChangeSet();

		coinItemChanges
			.OnItemAdded(item => item.IsSelected = !item.Coin.IsExcludedFromCoinJoin)
			.Subscribe()
			.DisposeWith(disposables);

		coinItemChanges
			.WhenPropertyChanged(x => x.IsSelected)
			.Select(_ => CoinList.Selection.Count > 0)
			.BindTo(this, x => x.HasSelection)
			.DisposeWith(disposables);

		CoinList.Selection
			.ToObservableChangeSet()
			.ToCollection()
			.Throttle(TimeSpan.FromMilliseconds(100), RxApp.MainThreadScheduler)
			.Select(_ => CoinList.CoinItems.Where(x => x.IsSelected != true).Select(x => x.Coin).ToArray())
			.DoAsync(async excluded => await _wallet.Coins.UpdateExcludedCoinsFromCoinjoinAsync(excluded))
			.Subscribe()
			.DisposeWith(disposables);

		_wallet.Coinjoin?.WhenAnyValue(x => x.IsCoinjoining)
			.Subscribe(isCoinjoining =>
			{
				CoinList.CoinItems.ForEach(y =>
				{
					var wasSelected = y.IsSelected;
					y.CanBeSelected = !isCoinjoining;
					y.IsSelected = wasSelected;
				});
				IsCoinjoining = isCoinjoining;
			})
			.DisposeWith(disposables);

		CoinList.DisposeWith(disposables);
	}

	private void SelectAll(bool value)
	{
		foreach (var coin in CoinList.CoinItems)
		{
			coin.IsSelected = value;
		}
	}
}
