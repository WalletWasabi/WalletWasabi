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

	[AutoNotify] private OrderViewModel? _selectedOrder;
	private readonly BuyAnythingManager _buyAnythingManager;
	private readonly Country[] _counties;

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
			IsBusy = false;
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
				await CreateAndAddEmptyOrderAsync(_cts.Token);
			}

			Observable
				.FromEventPattern<ConversationUpdateEvent>(buyAnythingManager,
					nameof(BuyAnythingManager.ConversationUpdated))
				.Select(args => args.EventArgs)
				.Where(e => e.Conversation.Id.WalletId == BuyAnythingManager.GetWalletId(_wallet))
				.ObserveOn(RxApp.MainThreadScheduler)
				.SubscribeAsync(async e =>
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
						await CreateAndAddEmptyOrderAsync(_cts.Token);
					}

					if (Orders.FirstOrDefault(x => x.BackendId == e.Conversation.Id) is { } orderToUpdate)
					{
						await orderToUpdate.UpdateOrderAsync(
							e.Conversation.Id,
							e.Conversation.ConversationStatus.ToString(),
							e.Conversation.OrderStatus.ToString(),
							CreateMessages(e.Conversation),
							e.Conversation.MetaData,
							cancellationToken);

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

		await CreateOrdersAsync(currentConversations.ToList(), cancellationToken);
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

	private async Task CreateOrdersAsync(IReadOnlyList<Conversation> conversations, CancellationToken cancellationToken)
	{
		var orders = new List<OrderViewModel>();

		for (int i = 0; i < conversations.Count; i++)
		{
			var conversation = conversations[i];
			orders.Add(await CreateOrderAsync(conversation, i, cancellationToken));
		}

		_ordersCache.AddOrUpdate(orders);
	}

	private async Task<OrderViewModel> CreateOrderAsync(Conversation conversation, int id, CancellationToken cancellationToken)
	{
		var order = new OrderViewModel(
			UiContext,
			id,
			conversation.MetaData,
			conversation.ConversationStatus.ToString(),
			new ShopinBitWorkflowManagerViewModel(BuyAnythingManager.GetWalletId(_wallet), _counties),
			this,
			cancellationToken);

		order.WorkflowManager.UpdateId(conversation.Id);

		var orderMessages = CreateMessages(conversation);
		order.UpdateMessages(orderMessages);

		await order.StartConversationAsync(conversation.ConversationStatus.ToString(), conversation.MetaData.Country);

		return order;
	}

	private async Task CreateAndAddEmptyOrderAsync(CancellationToken cancellationToken)
	{
		var walletId = BuyAnythingManager.GetWalletId(_wallet);

		if (Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			var nextId = Orders.Max(x => x.Id) + 1;
			var title = $"Order {buyAnythingManager.GetNextConversationId(walletId)}";

			var order = new OrderViewModel(
				UiContext,
				nextId,
				new ConversationMetaData(title, null),
				"Started",
				new ShopinBitWorkflowManagerViewModel(BuyAnythingManager.GetWalletId(_wallet), _counties),
				this,
				cancellationToken);

			await order.StartConversationAsync("Started", null);

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
