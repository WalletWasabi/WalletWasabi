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
	private readonly SourceCache<OrderViewModel, ConversationId> _ordersCache;
	private readonly BehaviorSubject<ConversationId> _updateTriggerSubject;

	private Country[] _countries;

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

	public void Activate(CompositeDisposable disposable)
	{
		Task.Run(async () =>
		{
			await InitializeCountries(_cts.Token);

			// TODO: Run Demo() for testing UI otherwise InitializeOrdersAsync(...)
#if false
			Demo(_cts.Token);
#else
			await InitializeOrdersAsync(_cts.Token, disposable);
#endif
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

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);
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

			if (_orders.Count == 0)
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
					// e.ConversationId
					// e.ChatMessages
					// TODO: Update the conversations.

					// The conversation belongs to the "fake" empty conversation
					if (Orders.All(x => x.Id != e.Conversation.Id))
					{
						// Update the fake conversation ID because now we have a valid one.
						// After updating the ID we can now create a new "fake" conversation.
						// We cannot have two fake conversation at a time, because we cannot distinguish them due the missing proper ID.
						CreateAndAddEmptyOrder(_cts.Token);
					}

					// Notify that conversation updated.
					_updateTriggerSubject.OnNext(e.Conversation.Id);
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
			UiContext,
			conversation.Id,
			$"Order {i + 1}",
			new ShopinBitWorkflowManagerViewModel(conversation.Id, _countries),
			this,
			cancellationToken);

		var orderMessages = CreateMessages(conversation);

		order.UpdateMessages(orderMessages);

		return order;
	}

	private void CreateAndAddEmptyOrder(CancellationToken cancellationToken)
	{
		var walletId = BuyAnythingManager.GetWalletId(_wallet);
		var nextOrderIndex = Orders.Count + 1;

		var order = new OrderViewModel(
			UiContext,
			new ConversationId(walletId, "", ""),
			$"Order {nextOrderIndex}",
			new ShopinBitWorkflowManagerViewModel(ConversationId.Empty, _countries),
			this,
			cancellationToken);

		_ordersCache.AddOrUpdate(order);
	}

	private void Demo(CancellationToken cancellationToken)
	{
		var walletId = BuyAnythingManager.GetWalletId(_wallet);

		var demoOrders = new[]
		{
			new OrderViewModel(
				UiContext,
				new ConversationId(walletId, "a", "a"),
				"Order 1",
				new ShopinBitWorkflowManagerViewModel(ConversationId.Empty, _countries),
				this,
				cancellationToken),
			new OrderViewModel(
				UiContext,
				new ConversationId(walletId, "b", "b"),
				"Order 2",
				new ShopinBitWorkflowManagerViewModel(ConversationId.Empty, _countries),
				this,
				cancellationToken),
			new OrderViewModel(
				UiContext,
				new ConversationId(walletId, "c", "d"),
				"Order 3",
				new ShopinBitWorkflowManagerViewModel(ConversationId.Empty, _countries),
				this,
				cancellationToken),
		};

		_ordersCache.AddOrUpdate(demoOrders);
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

	void IOrderManager.RemoveOrder(ConversationId id)
	{
		// TODO: Shouldn't this also remove from manager?
		_ordersCache.Edit(x =>
		{
			_ordersCache.RemoveKey(id);
		});

		SelectedOrder = _orders.FirstOrDefault();
	}
}
