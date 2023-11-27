using System.Reactive.Disposables;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

[NavigationMetaData(
	Title = "Buy",
	Caption = "Display wallet buy dialog",
	IconName = "wallet_action_buy",
	Order = 7,
	Category = "Wallet",
	Keywords = new[] { "Wallet", "Buy", "Action", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = false)]
public partial class BuyViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;

	public BuyViewModel(UiContext uiContext, WalletViewModel walletVm)
	{
		UiContext = uiContext;
		WalletVm = walletVm;

		_wallet = walletVm.Wallet;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
	}

	public WalletViewModel WalletVm { get; }

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(inHistory, disposables);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);
	}
}
