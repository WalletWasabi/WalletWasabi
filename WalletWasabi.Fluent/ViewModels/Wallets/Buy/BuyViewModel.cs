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
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;
using WalletWasabi.BuyAnything;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Binding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.ShopWare.Models;
using Country = WalletWasabi.BuyAnything.Country;

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
	private readonly Country[] _counties;

	[AutoNotify] private OrderViewModel? _selectedOrder;

	public BuyViewModel(UiContext uiContext, WalletViewModel walletVm)
	{
		IsBusy = true;
		UiContext = uiContext;
		WalletVm = walletVm;

		_wallet = walletVm.Wallet;
		_buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();
		_counties = _buyAnythingManager.Countries.ToArray();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		_ordersCache = new SourceCache<OrderViewModel, int>(x => x.Id);

		_ordersCache
			.Connect()
			.Sort(SortExpressionComparer<OrderViewModel>.Descending(x => x.Id))
			.Bind(out _orders)
			.Subscribe();

		_cts = new CancellationTokenSource();
	}

	public ReadOnlyObservableCollection<OrderViewModel> Orders => _orders;

	public WalletViewModel WalletVm { get; }

	public void Activate(CompositeDisposable disposable)
	{
		Task.Run(async () => { await InitializeAsync(disposable); }, _cts.Token);
	}

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(inHistory, disposables);

		MarkNewMessagesFromSelectedOrderAsRead().DisposeWith(disposables);
		SelectNewOrderIfAny();
	}

	private void SelectNewOrderIfAny()
	{
		// We should always have a new order, but this condition is for safety, just in case we change this axiom.
		if (Orders.FirstOrDefault(x => x.BackendId == ConversationId.Empty) is { } newOrder)
		{
			SelectedOrder = newOrder;
		}
	}

	private async Task InitializeAsync(CompositeDisposable disposable)
	{
		try
		{
			await InitializeOrdersAsync(_cts.Token, disposable);
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

	private async Task InitializeOrdersAsync(CancellationToken cancellationToken, CompositeDisposable disposable)
	{
		await UpdateOrdersAsync(cancellationToken, _buyAnythingManager);

		if (_orders.Count == 0 || _orders.All(x => x.BackendId != ConversationId.Empty))
		{
			await CreateAndAddEmptyOrderAsync(_cts.Token);
		}

		Observable
			.FromEventPattern<ConversationUpdateEvent>(_buyAnythingManager,
				nameof(BuyAnythingManager.ConversationUpdated))
			.Select(args => args.EventArgs)
			.Where(e => e.Conversation.Id.WalletId == BuyAnythingManager.GetWalletId(_wallet))
			.ObserveOn(RxApp.MainThreadScheduler)
			.SubscribeAsync(async e => { await OnConversationUpdated(e, cancellationToken); })
			.DisposeWith(disposable);
	}

	private async Task OnConversationUpdated(ConversationUpdateEvent e, CancellationToken cancellationToken)
	{
		var conversation = e.Conversation;

		// This handles the unbound conversation.
		// The unbound conversation is a conversation that only exists in the UI (yet)
		if (Orders.All(x => x.BackendId != e.Conversation.Id)) // If the update event belongs has an Id that doesn't match any of the existing orders
		{
			// It is because the incoming event has the freshly assigned BackedId.
			// We should lookup for the unbound order and assign its BackendId
			// and update it with the data in the conversation.
			var unboundOrder = Orders.First(x => x.BackendId == ConversationId.Empty);
			unboundOrder.WorkflowManager.UpdateConversationId(e.Conversation.Id); // The order is no longer unbound ;)

			// We cannot have two fake conversation at a time, because we cannot distinguish them due the missing proper ID.
			await CreateAndAddEmptyOrderAsync(_cts.Token);
		}

		if (Orders.FirstOrDefault(x => x.BackendId == conversation.Id) is { } orderToUpdate)
		{
			await orderToUpdate.UpdateOrderAsync(conversation, cancellationToken);

			Logger.LogDebug($"{nameof(BuyAnythingManager)}.{nameof(BuyAnythingManager.ConversationUpdated)}: OrderId={e.Conversation.Id.OrderId}, ConversationStatus={e.Conversation.ConversationStatus}, OrderStatus={e.Conversation.OrderStatus}");
		}
	}

	private async Task UpdateOrdersAsync(CancellationToken cancellationToken, BuyAnythingManager buyAnythingManager)
	{
		var walletId = BuyAnythingManager.GetWalletId(_wallet);
		var currentConversations = await buyAnythingManager.GetConversationsAsync(walletId, cancellationToken);

		await CreateAndAddOrdersAsync(currentConversations.ToList(), cancellationToken);
	}

	private async Task CreateAndAddOrdersAsync(IReadOnlyList<Conversation> conversations, CancellationToken cancellationToken)
	{
		var orders = new List<OrderViewModel>();

		for (int i = 0; i < conversations.Count; i++)
		{
			var conversation = conversations[i];
			orders.Add(await CreateOrderAsync(conversation, i, cancellationToken));
		}

		_ordersCache.AddOrUpdate(orders);
	}

	private Country? GetCountryFromConversation(Conversation conversation)
	{
		var countryName = conversation.ChatMessages.FirstOrDefault(x => x.MetaData.Tag == ChatMessageMetaData.ChatMessageTag.Country)?.Message;
		return _counties.FirstOrDefault(x => x.Name == countryName);
	}

	private async Task<OrderViewModel> CreateOrderAsync(Conversation conversation, int id, CancellationToken cancellationToken)
	{
		var order = new OrderViewModel(
			UiContext,
			id,
			conversation.MetaData,
			conversation.ConversationStatus.ToString(),
			new ShopinBitWorkflowManager(BuyAnythingManager.GetWalletId(_wallet), _counties),
			this,
			cancellationToken);

		order.WorkflowManager.UpdateConversationId(conversation.Id);
		order.UpdateMessages(conversation.ChatMessages);

		var country = GetCountryFromConversation(conversation);
		await order.StartConversationAsync(conversation.ConversationStatus.ToString(), country);

		return order;
	}

	private async Task CreateAndAddEmptyOrderAsync(CancellationToken cancellationToken)
	{
		var nextId = Orders.Count > 0 ? Orders.Max(x => x.Id) + 1 : 1;
		var title = "New Order";

		var order = new OrderViewModel(
			UiContext,
			nextId,
			new ConversationMetaData(title),
			"Started",
			new ShopinBitWorkflowManager(BuyAnythingManager.GetWalletId(_wallet), _counties),
			this,
			cancellationToken);

		await order.StartConversationAsync("Started", null);

		_ordersCache.AddOrUpdate(order);
	}

	async Task IOrderManager.RemoveOrderAsync(int id)
	{
		if (Orders.FirstOrDefault(x => x.Id == id) is { } orderToRemove)
		{
			await _buyAnythingManager.RemoveConversationsByIdsAsync(new[] { orderToRemove.BackendId }, _cts.Token);
		}

		_ordersCache.RemoveKey(id);
		SelectedOrder = _orders.FirstOrDefault();
	}
}
