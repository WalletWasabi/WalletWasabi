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
	private readonly SourceCache<OrderViewModel, Guid> _ordersCache;
	private readonly BehaviorSubject<OrderUpdateMessage> _updateTriggerSubject;

	private Country[] _countries;

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

		_cts = new CancellationTokenSource();

		// TODO: Do we want per-order triggers?
		_updateTriggerSubject = new BehaviorSubject<OrderUpdateMessage>(new OrderUpdateMessage(ConversationId.Empty, null, null));

		UpdateTrigger = _updateTriggerSubject;
	}

	public ReadOnlyObservableCollection<OrderViewModel> Orders => _orders;

	public WalletViewModel WalletVm { get; }

	public IObservable<OrderUpdateMessage> UpdateTrigger { get; }

	public void Activate(CompositeDisposable disposable)
	{
		Task.Run(async () =>
		{
			await InitializeCountries(_cts.Token);
			await InitializeOrdersAsync(_cts.Token, disposable);
			SelectedOrder = _orders.FirstOrDefault();
		}, _cts.Token);
	}

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
	}

	private async Task InitializeCountries(CancellationToken cancellationToken)
	{
		if (Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			_countries = await buyAnythingManager.GetCountriesAsync(cancellationToken);
		}
	}

	private async Task InitializeOrdersAsync(CancellationToken cancellationToken, CompositeDisposable disposable)
	{
		if (Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			// TODO: Fill up the UI with the conversations.
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
					// This handles the unbound conversation. The unbound conversation is a conversation that only exists in the UI (yet)

					if (Orders.All(x => x.BackendId != e.Conversation.Id)) // If the update event belongs has an Id that doesn't match any of the existing orders
					{
						// It is because the incoming event has the freshly assigned BackedId.
						// We should lookup for the unbound order and assign its BackendId and update it with the data in the conversation.
						var unboundOrder = Orders.First(x => x.BackendId == ConversationId.Empty);
						unboundOrder.Copy(e.Conversation);	// Copies the data from the updated conversation to the order
						unboundOrder.WorkflowManager.UpdateId(e.Conversation.Id); // The order is no longer unbound ;)

						// We cannot have two fake conversation at a time, because we cannot distinguish them due the missing proper ID.
						CreateAndAddEmptyOrder(_cts.Token);
						return;
					}

					// TODO: Handle OrderStatus and pass to order view model to handle.
					switch (e.Conversation.OrderStatus)
					{
						case OrderStatus.Open:
							break;
						case OrderStatus.Done:
							break;
						case OrderStatus.Cancelled:
							break;
						case OrderStatus.InProgress:
							break;
					}

					// TODO: Handle e.Conversation.Metadata

					// TODO: Update the order conversations using e.ChatMessages

					var orderMessages = CreateMessages(e.Conversation);

					// TODO: Handle ConversationStatus and pass to order view model to handle.
					switch (e.Conversation.ConversationStatus)
					{
						case ConversationStatus.Started:
							_updateTriggerSubject.OnNext(new OrderUpdateMessage(e.Conversation.Id, "Started", orderMessages));
							return;
						case ConversationStatus.OfferReceived:
							_updateTriggerSubject.OnNext(new OrderUpdateMessage(e.Conversation.Id, "OfferReceived", orderMessages));
							return;
						case ConversationStatus.PaymentDone:
							_updateTriggerSubject.OnNext(new OrderUpdateMessage(e.Conversation.Id, "PaymentDone", orderMessages));
							return;
						case ConversationStatus.PaymentConfirmed:
							_updateTriggerSubject.OnNext(new OrderUpdateMessage(e.Conversation.Id, "PaymentConfirmed", orderMessages));
							return;
						case ConversationStatus.OfferAccepted:
							_updateTriggerSubject.OnNext(new OrderUpdateMessage(e.Conversation.Id, "OfferAccepted", orderMessages));
							return;
						case ConversationStatus.InvoiceReceived:
							_updateTriggerSubject.OnNext(new OrderUpdateMessage(e.Conversation.Id, "InvoiceReceived", orderMessages));
							return;
					}

					// TODO: Get the command (if any) form agent message using e.Conversation.ChatMessages (ChatMessage does not have it?)
					var command = default(string);

					// Notify that conversation updated.
					_updateTriggerSubject.OnNext(new OrderUpdateMessage(e.Conversation.Id, command, orderMessages));
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
					// TODO: Check if message exists
					IsUnread = true
				};
				orderMessages.Add(userMessage);
			}
			else
			{
				var userMessage = new AssistantMessageViewModel(null, null)
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
			UiContext,
			Guid.NewGuid(),
			$"Order {i + 1}",
			new ShopinBitWorkflowManagerViewModel(_countries, BuyAnythingManager.GetWalletId(_wallet)),
			this,
			cancellationToken);

		order.WorkflowManager.UpdateId(conversation.Id);

		var orderMessages = CreateMessages(conversation);
		order.UpdateMessages(orderMessages);

		return order;
	}

	private void CreateAndAddEmptyOrder(CancellationToken cancellationToken)
	{
		var nextOrderIndex = Orders.Count + 1;

		var order = new OrderViewModel(
			UiContext,
			Guid.NewGuid(),
			$"Order {nextOrderIndex}",
			new ShopinBitWorkflowManagerViewModel(_countries, BuyAnythingManager.GetWalletId(_wallet)),
			this,
			cancellationToken);

		_ordersCache.AddOrUpdate(order);
	}

	bool IOrderManager.HasUnreadMessages(ConversationId id)
	{
		// TODO: Check if order had unread messages.
		return true;
	}

	bool IOrderManager.IsCompleted(ConversationId id)
	{
		// TODO: Check if order is completed.
		// -> Save manager as a field then manager.GetConversationsByIdAsync(id).IsCompleted() ?
		return false;
	}

	void IOrderManager.RemoveOrder(Guid id)
	{
		// TODO: Shouldn't this also remove from manager?
		_ordersCache.RemoveKey(id);
		SelectedOrder = _orders.FirstOrDefault();
	}
}
