using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
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
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;
using WalletWasabi.WebClients.BuyAnything;

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
	private readonly SourceCache<OrderViewModel, ConversationId> _ordersCache;
	private readonly BehaviorSubject<ConversationId> _updateTriggerSubject;

	[AutoNotify] private OrderViewModel? _selectedOrder;

	public BuyViewModel(UiContext uiContext, WalletViewModel walletVm)
	{
		UiContext = uiContext;
		WalletVm = walletVm;

		_wallet = walletVm.Wallet;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		_ordersCache = new SourceCache<OrderViewModel, ConversationId>(x => x.Id);

		_ordersCache
			.Connect()
			.Bind(out _orders)
			.Subscribe();

		_cts = new CancellationTokenSource();

		// TODO: Do we want per-order triggers?
		_updateTriggerSubject = new BehaviorSubject<ConversationId>(ConversationId.Empty);

		UpdateTrigger = _updateTriggerSubject;
	}

	public ReadOnlyObservableCollection<OrderViewModel> Orders => _orders;

	public WalletViewModel WalletVm { get; }

	public IObservable<ConversationId> UpdateTrigger { get; }

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(inHistory, disposables);

		// TODO: For testing.
		this.WhenAnyValue(x => x.SelectedOrder)
			.Subscribe(x =>
			{
				Task.Run(async () =>
				{
					await Task.Delay(500);
					Dispatcher.UIThread.Post(() => x?.Update());
				}, _cts.Token);
			})
			.DisposeWith(disposables);

		// TODO:
		Task.Run(async () =>
		{
			// TODO: Run Demo() for testing UI otherwise InitializeOrdersAsync(...)
#if true
			Demo(_cts.Token);
#else
			await InitializeOrdersAsync(_cts.Token);
#endif
			SelectedOrder = _orders.FirstOrDefault();
		}, _cts.Token);
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);
	}

	private async Task InitializeOrdersAsync(CancellationToken cancellationToken)
	{
		if (Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			// TODO: Fill up the UI with the conversations.
			await UpdateOrdersAsync(cancellationToken, buyAnythingManager);

			if (_orders.Count == 0)
			{
				var walletId = BuyAnythingManager.GetWalletId(_wallet);
				await buyAnythingManager.StartNewConversationAsync(walletId, "", BuyAnythingClient.Product.ConciergeRequest, "Hello World", cancellationToken);
				await UpdateOrdersAsync(cancellationToken, buyAnythingManager);
			}

			Observable
				.FromEventPattern<ConversationUpdateEvent>(buyAnythingManager,
					nameof(BuyAnythingManager.ConversationUpdated))
				.Select(args => args.EventArgs)
				.Where(e => e.Conversation.Id.WalletId == BuyAnythingManager.GetWalletId(_wallet))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e =>
				{
					// e.ConversationId
					// e.ChatMessages
					// TODO: Update the conversations.

					// Notify that conversation updated.
					_updateTriggerSubject.OnNext(e.Conversation.Id);
				});
		}
	}

	private async Task UpdateOrdersAsync(CancellationToken cancellationToken, BuyAnythingManager buyAnythingManager)
	{
		var currentConversations = await buyAnythingManager.GetConversationsAsync(_wallet, cancellationToken);

		CreateOrders(currentConversations.ToList(), cancellationToken);
	}

	private List<MessageViewModel> CreateMessages(Conversation conversation)
	{
		var orderMessages = new List<MessageViewModel>();

		foreach (var message in conversation.Messages)
		{
			if (message.IsMyMessage)
			{
				var userMessage = new UserMessageViewModel()
				{
					Message = message.Message,
					// TODO: Check if message exists
					IsUnread = true
				};
				orderMessages.Add(userMessage);
			}
			else
			{
				var userMessage = new AssistantMessageViewModel()
				{
					Message = message.Message,
					// TODO: Check if message exists
					IsUnread = true
				};
				orderMessages.Add(userMessage);
			}
		}

		return orderMessages;
	}

	private void CreateOrders(List<Conversation> conversations, CancellationToken cancellationToken)
	{
		var orders = new List<OrderViewModel>();

		for (var i = 0; i < conversations.Count; i++)
		{
			var conversation = conversations[i];
			var order = CreateOrder(conversation, cancellationToken, i);
			orders.Add(order);
		}

		_ordersCache.AddOrUpdate(orders);
	}

	private OrderViewModel CreateOrder(Conversation conversation, CancellationToken cancellationToken, int i)
	{
		// TODO: Conversation needs name/title?
		var order = new OrderViewModel(
			conversation.Id,
			$"Order {i + 1}",
			new ShopinBitWorkflowManagerViewModel(conversation.Id),
			this,
			cancellationToken);

		var orderMessages = CreateMessages(conversation);

		order.UpdateMessages(orderMessages);

		return order;
	}

	private void Demo(CancellationToken cancellationToken)
	{
		var demoOrders = new[]
		{
			new OrderViewModel(new ConversationId("1", "", ""), "Order 1", new ShopinBitWorkflowManagerViewModel(ConversationId.Empty), this, cancellationToken),
			new OrderViewModel(new ConversationId("2", "", ""), "Order 2", new ShopinBitWorkflowManagerViewModel(ConversationId.Empty), this, cancellationToken),
			new OrderViewModel(new ConversationId("3", "", ""), "Order 3", new ShopinBitWorkflowManagerViewModel(ConversationId.Empty), this, cancellationToken),
		};

		_ordersCache.AddOrUpdate(demoOrders);
	}

	bool IOrderManager.HasUnreadMessages(ConversationId id)
	{
		// TODO: Check if order had unread messages.
		return true;
	}

	bool IOrderManager.IsCompleted(ConversationId idS)
	{
		// TODO: Check if order is completed.
		return false;
	}

	void IOrderManager.RemoveOrder(ConversationId id)
	{
		_ordersCache.Edit(x =>
		{
			_ordersCache.RemoveKey(id);
		});

		SelectedOrder = _orders.FirstOrDefault();
	}
}
