using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced;

[NavigationMetaData(
	Title = "Wallet Stats",
	Caption = "Display wallet stats",
	IconName = "nav_wallet_24_regular",
	Order = 3,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Stats", },
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
