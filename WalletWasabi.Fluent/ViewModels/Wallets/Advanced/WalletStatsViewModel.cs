using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(
	Order = 3,
	Category = SearchCategory.Wallet,
	Title = "WalletStatsViewModel_Title",
	Caption = "WalletStatsViewModel_Caption",
	Keywords = "WalletStatsViewModel_Keywords",
	IconName = "nav_wallet_24_regular",
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class WalletStatsViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private IWalletStatsModel? _model;

	private WalletStatsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;

		NextCommand = ReactiveCommand.Create(() => Navigate().Clear());
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		Model = _wallet.GetWalletStats().DisposeWith(disposables);
	}
}
