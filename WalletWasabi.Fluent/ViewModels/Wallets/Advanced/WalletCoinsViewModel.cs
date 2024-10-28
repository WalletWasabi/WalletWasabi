using System.Collections.Generic;
using System.Reactive.Disposables;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(
	IconName = "nav_wallet_24_regular",
	Order = 0,
	Category = SearchCategory.Wallet,
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	IsLocalized = true,
	Searchable = false)]
public partial class WalletCoinsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	public WalletCoinsViewModel(UiContext uiContext, IWalletModel wallet)
	{
		UiContext = uiContext;
		_wallet = wallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = CancelCommand;
		CoinList = new CoinListViewModel(_wallet.Coins, new List<ICoinModel>(), allowCoinjoiningCoinSelection: false, ignorePrivacyMode: false, allowSelection: false);
	}

	public CoinListViewModel CoinList { get; }

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
