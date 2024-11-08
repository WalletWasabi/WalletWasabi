using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

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
	private readonly ReadOnlyObservableCollection<OrderViewModel> _orders;
	private readonly SourceCache<OrderViewModel, int> _ordersCache;
	private readonly IWalletModel _wallet;

	[AutoNotify] private OrderViewModel? _selectedOrder;

	public BuyViewModel(UiContext uiContext, IWalletModel wallet)
	{
		IsBusy = true;
		UiContext = uiContext;
		_wallet = wallet;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		_ordersCache = new SourceCache<OrderViewModel, int>(x => x.OrderNumber);

		_ordersCache
			.Connect()
			.Sort(SortExpressionComparer<OrderViewModel>.Descending(x => x.OrderNumber))
			.DisposeMany()
			.Bind(out _orders)
			.Subscribe();

		_cts = new CancellationTokenSource();

		HasRelevantOrder = this.WhenAnyValue(x => x.Orders.Count)
			.Select(_ => Orders.Any(x => x.Workflow.Conversation.OrderStatus is not OrderStatus.Cancelled));

		Activate();

	}

	public IObservable<bool> HasRelevantOrder { get; }

	public ReadOnlyObservableCollection<OrderViewModel> Orders => _orders;

	async Task IOrderManager.RemoveOrderAsync(int id)
	{
		if (Orders.FirstOrDefault(x => x.OrderNumber == id) is { } orderToRemove)
		{
			await _wallet.BuyAnything.RemoveConversationByIdAsync(orderToRemove.ConversationId, _cts.Token);
		}

		_ordersCache.RemoveKey(id);
		if (_orders.Count > 0)
		{
			SelectedOrder = _orders.First();
		}
		else
		{
			CancelCommand.ExecuteIfCan();
		}
	}

	public async Task OnError(Exception ex)
	{
		if (!IsActive)
		{
			return;
		}

		await ShowErrorAsync(
			"Buy Anything",
			ex.ToUserFriendlyString(),
			"",
			NavigationTarget.CompactDialogScreen);
	}

	public void Activate()
	{
		Task.Run(async () => await InitializeAsync(), _cts.Token);
	}

	protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(inHistory, disposables);

		SelectedOrder = Orders.FirstOrDefault();

		// Mark Conversation as read for selected order
		this.WhenAnyValue(x => x.SelectedOrder, x => x.SelectedOrder.Workflow.Conversation, (order, _) => order)
			.WhereNotNull()
			.DoAsync(order => order.MarkAsReadAsync())
			.Subscribe()
			.DisposeWith(disposables);

		MarkNewMessagesFromSelectedOrderAsRead().DisposeWith(disposables);
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

	private async Task InitializeAsync()
	{
		try
		{
			var currentConversations = await _wallet.BuyAnything.GetConversationsAsync(_cts.Token);

			var orders =
				currentConversations.Select((conversation, index) => new OrderViewModel(UiContext, _wallet, conversation, this, index))
								    .ToArray();


			_ordersCache.AddOrUpdate(orders);
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
}
