using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(
	Title = "Wallet Coins",
	Caption = "Display wallet coins",
	IconName = "nav_wallet_24_regular",
	Order = 0,
	Category = "Wallet",
	Keywords = ["Wallet", "Coins", "UTXO"],
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class WalletCoinsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	[AutoNotify] private CoinListViewModel _coinList;

	[AutoNotify] private IObservable<bool> _isAnySelected = Observable.Return(false);

	private WalletCoinsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;
		
		_coinList = new CoinListViewModel(_wallet, _wallet.Coins, new List<ICoinModel>());
		IsAnySelected = CoinList.Selection.ToObservableChangeSet().Count().Select(i => i > 0);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		if (!isInHistory)
		{
			CoinList.ExpandAllCommand.Execute().Subscribe().DisposeWith(disposables);
		}
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		if (!isInHistory)
		{
			CoinList.Dispose();
		}
	}
}
