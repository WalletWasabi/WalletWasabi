using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[AppLifetime]
[NavigationMetaData(Title = "Excluded Coins", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ExcludedCoinsViewModel : DialogViewModelBase<Unit>
{
	private readonly IWalletModel _wallet;
	private readonly CompositeDisposable _disposables = new();

	[AutoNotify] private bool? _areAllCoinsSelected;

	public ExcludedCoinsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		CoinList = new CoinListViewModel(wallet, wallet.ExcludedCoins.ToList(), true);
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = ReactiveCommand.Create(
			() =>
			{
				ExcludeSelectedCoins();
				Close();
			});

		ToggleSelectionCommand = ReactiveCommand.Create(() => SelectAll(!CoinList.Selection.Any()));
	}

	private void SelectAll(bool value)
	{
		foreach (var coin in CoinList.CoinItems)
		{
			coin.IsSelected = value;
		}
	}
	
	public ICommand ToggleSelectionCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		CoinList.CoinItems
			.ToObservableChangeSet()
			.WhenPropertyChanged(x => x.IsSelected)
			.Select(_ => CoinList.Selection.Count == CoinList.CoinItems.Count ? true : CoinList.Selection.Count == 0 ? false : (bool?)null)
			.BindTo(this, x => x.AreAllCoinsSelected)
			.DisposeWith(_disposables);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		if (!isInHistory)
		{
			_disposables.Dispose();
		}
	}

	public CoinListViewModel CoinList { get; set; }

	private void ExcludeSelectedCoins()
	{
		_wallet.ExcludedCoins = CoinList.Selection;
	}
}
