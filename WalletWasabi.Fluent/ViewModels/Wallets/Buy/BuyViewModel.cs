using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.BuyAnything;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Binding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Logging;
using Country = WalletWasabi.BuyAnything.Country;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

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
public partial class BuyViewModel : RoutableViewModel, IOrderManager
{
	private readonly CancellationTokenSource _cts;
	private readonly Wallet _wallet;
	private readonly ReadOnlyObservableCollection<OrderViewModel> _orders;
	private readonly SourceCache<OrderViewModel, int> _ordersCache;
	private readonly BuyAnythingManager _buyAnythingManager;
	private readonly Country[] _countries;

	[AutoNotify] private OrderViewModel? _selectedOrder;
	[AutoNotify] private OrderViewModel? _emptyOrder; // Used to track the "Empty" order (with empty ConversationId)

	public BuyViewModel(UiContext uiContext, WalletViewModel walletVm)
	{
		IsBusy = true;
		UiContext = uiContext;
		WalletVm = walletVm;

		_wallet = walletVm.Wallet;
		_buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();
		_countries = _buyAnythingManager.Countries.ToArray();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		_ordersCache = new SourceCache<OrderViewModel, int>(x => x.OrderNumber);

		_ordersCache
			.Connect()
			.Sort(SortExpressionComparer<OrderViewModel>.Descending(x => x.OrderNumber))
			.Bind(out _orders)
			.Subscribe();

		_cts = new CancellationTokenSource();

		// When the Empty Order stops being empty, create a new Empty Order
		this.WhenAnyValue(x => x.EmptyOrder.Workflow.Conversation)
			.Where(x => x.Id != ConversationId.Empty)
			.Do(_ => EmptyOrder = NewEmptyOrder())
			.Subscribe();
	}

	public ReadOnlyObservableCollection<OrderViewModel> Orders => _orders;

	public WalletViewModel WalletVm { get; }

	public void Activate(CompositeDisposable disposable)
	{
		Task.Run(async () => await InitializeAsync(disposable), _cts.Token);
	}

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(inHistory, disposables);

		// Mark Conversation as read for selected order
		this.WhenAnyValue(x => x.SelectedOrder)
			.WhereNotNull()
			.DoAsync(async order => await order.MarkAsReadAsync())
			.Subscribe()
			.DisposeWith(disposables);

		MarkNewMessagesFromSelectedOrderAsRead().DisposeWith(disposables);

		SelectNewOrderIfAny();
	}

	private void SelectNewOrderIfAny()
	{
		// We should always have an empty order, but this condition is for safety, just in case we change this axiom.
		if (EmptyOrder is { } emptyOrder)
		{
			SelectedOrder = emptyOrder;
		}
	}

	private IDisposable MarkNewMessagesFromSelectedOrderAsRead()
	{
		return this
			.WhenAnyValue(x => x.SelectedOrder)
			.WhereNotNull()
			.Select(x => x.Messages.ToObservableChangeSet())
			.Switch()
			.OnItemAdded(x => x.IsUnread = false)
			.Subscribe();
	}

	private async Task InitializeAsync(CompositeDisposable disposable)
	{
		try
		{
			var currentConversations = await _buyAnythingManager.GetConversationsAsync(_wallet, _cts.Token);

			var orders =
				currentConversations.Select((conversation, index) =>
				{
					var workflow = Workflow.Create(_wallet, conversation);
					var order = new OrderViewModel(UiContext, workflow, this, index, _cts.Token);
					return order;
				})
				.ToArray();

			_ordersCache.AddOrUpdate(orders);

			if (_orders.Count == 0 || _orders.All(x => x.ConversationId != ConversationId.Empty))
			{
				EmptyOrder = NewEmptyOrder();
			}

			SelectedOrder = _orders.FirstOrDefault();
		}
		catch (Exception exception)
		{
			Logger.LogError($"Error while initializing orders: {exception}).");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private OrderViewModel NewEmptyOrder()
	{
		var nextOrderNumber = Orders.Count > 0 ? Orders.Max(x => x.OrderNumber) + 1 : 1;
		var title = "New Order";

		var conversation = new Conversation(ConversationId.Empty, Chat.Empty, OrderStatus.Open, ConversationStatus.Started, new ConversationMetaData(title));

		var workflow = Workflow.Create(_wallet, conversation);

		var order = new OrderViewModel(UiContext, workflow, this, nextOrderNumber, _cts.Token);

		_ordersCache.AddOrUpdate(order);

		return order;
	}

	async Task IOrderManager.RemoveOrderAsync(int id)
	{
		if (Orders.FirstOrDefault(x => x.OrderNumber == id) is { } orderToRemove)
		{
			await _buyAnythingManager.RemoveConversationsByIdsAsync(new[] { orderToRemove.ConversationId }, _cts.Token);
		}

		_ordersCache.RemoveKey(id);
		SelectedOrder = _orders.FirstOrDefault();
	}
}
