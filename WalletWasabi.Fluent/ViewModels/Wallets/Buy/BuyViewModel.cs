using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia.Threading;
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
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;
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

	private Country[] _countries;

	[AutoNotify] private OrderViewModel? _selectedOrder;

	public BuyViewModel(UiContext uiContext, WalletViewModel walletVm)
	{
		UiContext = uiContext;
		WalletVm = walletVm;

		_wallet = walletVm.Wallet;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		_ordersCache = new SourceCache<OrderViewModel, int>(x => x.Id);

		_ordersCache
			.Connect()
			.Sort(SortExpressionComparer<OrderViewModel>.Descending(x => x.Title))
			.Bind(out _orders)
			.Subscribe();

		_cts = new CancellationTokenSource();
	}

	public ReadOnlyObservableCollection<OrderViewModel> Orders => _orders;

	public WalletViewModel WalletVm { get; }

	public void Activate(CompositeDisposable disposable)
	{
		Task.Run(async () =>
		{
			await InitializeOrdersAsync(_cts.Token, disposable);
			SelectedOrder = _orders.FirstOrDefault();
		}, _cts.Token);
	}

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(inHistory, disposables);

		MarkNewMessagesFromSelectedOrderAsRead().DisposeWith(disposables);
	}

	private IDisposable MarkNewMessagesFromSelectedOrderAsRead() => this.WhenAnyValue(x => x.SelectedOrder)
		.WhereNotNull()
		.Select(x => x.Messages.ToObservableChangeSet())
		.Switch()
		.OnItemAdded(x => x.IsUnread = false)
		.Subscribe();

	private async Task InitializeOrdersAsync(CancellationToken cancellationToken, CompositeDisposable disposable)
	{
		if (Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			await UpdateOrdersAsync(cancellationToken, buyAnythingManager);

			if (_orders.Count == 0 || _orders.All(x => x.BackendId != ConversationId.Empty))
			{
				CreateAndAddEmptyOrder(_cts.Token);
			}

			Observable
				.FromEventPattern<ConversationUpdateEvent>(buyAnythingManager,
					nameof(BuyAnythingManager.ConversationUpdated))
				.Select(args => args.EventArgs)
				.Where(e => e.Conversation.Id.WalletId == BuyAnythingManager.GetWalletId(_wallet))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e =>
				{
					// This handles the unbound conversation.
					// The unbound conversation is a conversation that only exists in the UI (yet)

					if (Orders.All(x => x.BackendId != e.Conversation.Id)) // If the update event belongs has an Id that doesn't match any of the existing orders
					{
						// It is because the incoming event has the freshly assigned BackedId.
						// We should lookup for the unbound order and assign its BackendId
						// and update it with the data in the conversation.
						var unboundOrder = Orders.First(x => x.BackendId == ConversationId.Empty);
						unboundOrder.WorkflowManager.UpdateId(e.Conversation.Id); // The order is no longer unbound ;)

						// We cannot have two fake conversation at a time, because we cannot distinguish them due the missing proper ID.
						CreateAndAddEmptyOrder(_cts.Token);
					}

					if (Orders.FirstOrDefault(x => x.BackendId == e.Conversation.Id) is { } orderToUpdate)
					{
						orderToUpdate.UpdateOrder(
							e.Conversation.Id,
							e.Conversation.ConversationStatus.ToString(),
							e.Conversation.OrderStatus.ToString(),
							CreateMessages(e.Conversation),
							e.Conversation.MetaData);

						Logging.Logger.LogDebug($"{nameof(BuyAnythingManager)}.{nameof(BuyAnythingManager.ConversationUpdated)}: OrderId={e.Conversation.Id.OrderId}, ConversationStatus={e.Conversation.ConversationStatus}, OrderStatus={e.Conversation.OrderStatus}");
					}
				})
				.DisposeWith(disposable);
		}
	}

	private async Task UpdateOrdersAsync(CancellationToken cancellationToken, BuyAnythingManager buyAnythingManager)
	{
		var walletId = BuyAnythingManager.GetWalletId(_wallet);
		var currentConversations = await buyAnythingManager.GetConversationsAsync(walletId, cancellationToken);

		CreateOrders(currentConversations.ToList(), cancellationToken);
	}

	private List<MessageViewModel> CreateMessages(Conversation conversation)
	{
		var orderMessages = new List<MessageViewModel>();

		foreach (var message in conversation.ChatMessages)
		{
			if (message.IsMyMessage)
			{
				var userMessage = new UserMessageViewModel(null, null, null)
				{
					Message = message.Message,
					IsUnread = message.IsUnread
				};
				orderMessages.Add(userMessage);
			}
			else
			{
				var userMessage = new AssistantMessageViewModel(null, null)
				{
					Message = message.Message,
					IsUnread = message.IsUnread
				};
				orderMessages.Add(userMessage);
			}
		}

		return orderMessages;
	}

	private void CreateOrders(IReadOnlyList<Conversation> conversations, CancellationToken cancellationToken)
	{
		var orderViewModels = conversations.Select((x, i) => CreateOrder(x, i, cancellationToken));
		_ordersCache.AddOrUpdate(orderViewModels);
	}

	private OrderViewModel CreateOrder(Conversation conversation, int id, CancellationToken cancellationToken)
	{
		var order = new OrderViewModel(
			UiContext,
			id,
			conversation.MetaData,
			conversation.ConversationStatus.ToString(),
			new ShopinBitWorkflowManagerViewModel(_countries, BuyAnythingManager.GetWalletId(_wallet)),
			this,
			cancellationToken);

		order.WorkflowManager.UpdateId(conversation.Id);

		var orderMessages = CreateMessages(conversation);
		order.UpdateMessages(orderMessages);

		order.StartConversation(conversation.ConversationStatus.ToString());

		return order;
	}

	private void CreateAndAddEmptyOrder(CancellationToken cancellationToken)
	{
		var walletId = BuyAnythingManager.GetWalletId(_wallet);

		if (Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			var title = $"Order {buyAnythingManager.GetNextConversationId(walletId)}";

			var order = new OrderViewModel(
				UiContext,
				_orders.Count,
				new ConversationMetaData(title, null),
				"Started",
				new ShopinBitWorkflowManagerViewModel(_countries, BuyAnythingManager.GetWalletId(_wallet)),
				this,
				cancellationToken);

			order.StartConversation("Started");

			_ordersCache.AddOrUpdate(order);
		}
	}

	async Task IOrderManager.RemoveOrderAsync(int id)
	{
		if (Orders.FirstOrDefault(x => x.Id == id) is { } orderToRemove && Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			await buyAnythingManager.RemoveConversationsByIdsAsync(new[] { orderToRemove.BackendId }, _cts.Token);
		}

		_ordersCache.RemoveKey(id);
		SelectedOrder = _orders.FirstOrDefault();
	}
}
