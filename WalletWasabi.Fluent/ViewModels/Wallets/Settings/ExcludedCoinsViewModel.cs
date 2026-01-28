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

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[NavigationMetaData(Title = "Exclude Coins", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ExcludedCoinsViewModel : DialogViewModelBase<Unit>
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private bool _hasSelection;
	[AutoNotify] private bool _isCoinjoining;

	public ExcludedCoinsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		var initialCoins = wallet.Coins.List.Items.Where(x => x.IsExcludedFromCoinJoin);
		CoinList = new CoinListViewModel(wallet.Coins, initialCoins.ToList(), allowCoinjoiningCoinSelection: false, ignorePrivacyMode: true);
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = ReactiveCommand.Create(() => Close());
		ToggleSelectionCommand = ReactiveCommand.Create(() => SelectAll(!CoinList.Selection.Any()));
	}

	public CoinListViewModel CoinList { get; set; }

	public ICommand ToggleSelectionCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		CoinList.CoinItems
			.ToObservableChangeSet()
			.WhenPropertyChanged(x => x.IsSelected)
			.Select(_ => CoinList.Selection.Count > 0)
			.BindTo(this, x => x.HasSelection)
			.DisposeWith(disposables);

		CoinList.Selection
			.ToObservableChangeSet()
			.ToCollection()
			.Throttle(TimeSpan.FromMilliseconds(100), RxApp.MainThreadScheduler)
			.DoAsync(async x => await _wallet.Coins.UpdateExcludedCoinsFromCoinjoinAsync(x.ToArray()))
			.Subscribe()
			.DisposeWith(disposables);

		_wallet.Coinjoin?.WhenAnyValue(x => x.IsCoinjoining)
			.Subscribe(x =>
			{
				CoinList.CoinItems.ForEach(y =>
				{
					var wasSelected = y.IsSelected;
					y.CanBeSelected = !x;
					y.IsSelected = wasSelected;
				});
				IsCoinjoining = x;
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
