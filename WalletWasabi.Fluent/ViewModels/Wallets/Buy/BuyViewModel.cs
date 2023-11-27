using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

[NavigationMetaData(
	Title = "Buy Anything",
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
	private readonly ReadOnlyObservableCollection<OrderViewModel> _orders;
	private readonly SourceCache<OrderViewModel,Guid> _ordersCache;

	[AutoNotify] private OrderViewModel? _selectedOrder;

	public BuyViewModel(UiContext uiContext, WalletViewModel walletVm)
	{
		UiContext = uiContext;
		WalletVm = walletVm;

		_wallet = walletVm.Wallet;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		_ordersCache = new SourceCache<OrderViewModel, Guid>(x => x.Id);

		_ordersCache
			.Connect()
			.Bind(out _orders)
			.Subscribe();

		Demo();

		SelectedOrder = _orders.FirstOrDefault();
	}

	public ReadOnlyObservableCollection<OrderViewModel> Orders => _orders;

	public WalletViewModel WalletVm { get; }

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(inHistory, disposables);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);
	}

	private void Demo()
	{
		var demoOrders = new[]
		{
			new OrderViewModel(Guid.NewGuid())
			{
				Title = "Order 001",
			},
			new OrderViewModel(Guid.NewGuid())
			{
				Title = "Order 002",
			},
			new OrderViewModel(Guid.NewGuid())
			{
				Title = "Order 003",
			}
		};

		_ordersCache.AddOrUpdate(demoOrders);
	}
}
