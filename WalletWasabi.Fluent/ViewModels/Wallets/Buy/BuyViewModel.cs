using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
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
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

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
	private readonly Wallet _wallet;
	private readonly ReadOnlyObservableCollection<OrderViewModel> _orders;
	private readonly SourceCache<OrderViewModel, string> _ordersCache;
	private readonly BehaviorSubject<string> _updateTriggerSubject;

	[AutoNotify] private OrderViewModel? _selectedOrder;

	public BuyViewModel(UiContext uiContext, WalletViewModel walletVm)
	{
		UiContext = uiContext;
		WalletVm = walletVm;

		_wallet = walletVm.Wallet;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		_ordersCache = new SourceCache<OrderViewModel, string>(x => x.ContextToken);

		_ordersCache
			.Connect()
			.Bind(out _orders)
			.Subscribe();

		// TODO: Do we want per-order triggers?
		_updateTriggerSubject = new BehaviorSubject<string>(string.Empty);

		UpdateTrigger = _updateTriggerSubject;

		// Demo();

		// TODO: Move to OnNavigatedTo ?
		if (Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			// TODO: Fill up the UI with the conversations.
			var currentConversations = buyAnythingManager.GetConversations(_wallet);

			CreateOrders(currentConversations);

			Observable
				.FromEventPattern<ConversationUpdateEvent>(buyAnythingManager, nameof(BuyAnythingManager.ConversationUpdated))
				.Select(args => args.EventArgs)
				.Where(e => e.Wallet == _wallet)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(e =>
				{
					// e.ConversationId
					// e.ChatMessages
					// TODO: Where is the Code? Update the conversations.

					// Notify that conversation updated.
					_updateTriggerSubject.OnNext(e.ConversationId);
				});
		}
	}

	public ReadOnlyObservableCollection<OrderViewModel> Orders => _orders;

	public WalletViewModel WalletVm { get; }

	public IObservable<string> UpdateTrigger { get; }

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(inHistory, disposables);

		// TODO: For testing.
		this.WhenAnyValue(x => x.SelectedOrder)
			.Subscribe(x =>
			{
				Task.Run(async () =>
				{
					await Task.Delay(1000);
					Dispatcher.UIThread.Post(() => x?.Update());
				});
			})
			.DisposeWith(disposables);

		SelectedOrder = _orders.FirstOrDefault();
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);
	}

	private void CreateOrders(IEnumerable<Conversation> currentConversations)
	{
		var orders = new List<OrderViewModel>();

		foreach (var conversation in currentConversations)
		{
			// TODO: Conversation needs name/title?
			var order = new OrderViewModel(
				conversation.ContextToken,
				"Order ??",
				new ShopinBitWorkflowManagerViewModel(),
				this);

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

			order.UpdateMessages(orderMessages);
		}

		_ordersCache.AddOrUpdate(orders);
	}

	private void Demo()
	{
		var demoOrders = new[]
		{
			new OrderViewModel(Guid.NewGuid().ToString(), "Order 001", new ShopinBitWorkflowManagerViewModel(), this),
			new OrderViewModel(Guid.NewGuid().ToString(), "Order 002", new ShopinBitWorkflowManagerViewModel(), this),
			new OrderViewModel(Guid.NewGuid().ToString(), "Order 003", new ShopinBitWorkflowManagerViewModel(), this),
		};

		_ordersCache.AddOrUpdate(demoOrders);
	}

	bool IOrderManager.HasUnreadMessages(string contextToken)
	{
		// TODO: Check if order had unread messages.
		return true;
	}

	bool IOrderManager.IsCompleted(string contextToken)
	{
		// TODO: Check if order is completed.
		return false;
	}

	void IOrderManager.RemoveOrder(string contextToken)
	{
		_ordersCache.Edit(x =>
		{
			_ordersCache.RemoveKey(contextToken);
		});

		SelectedOrder = _orders.FirstOrDefault();
	}
}
