using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public partial class OrderViewModel : ViewModelBase
{
	private readonly ReadOnlyObservableCollection<MessageViewModel> _messages;
	private readonly SourceList<MessageViewModel> _messagesList;

	private readonly IOrderManager _orderManager;
	private readonly CancellationToken _cancellationToken;
	private readonly BuyAnythingManager _buyAnythingManager;

	[AutoNotify] private string _title;
	[AutoNotify] private bool _isBusy;
	[AutoNotify] private bool _isCompleted;
	[AutoNotify] private bool _hasUnreadMessages;
	[AutoNotify] private bool _isSelected;

	public OrderViewModel(UiContext uiContext, Workflow workflow, IOrderManager orderManager, int orderNumber, CancellationToken cancellationToken)
	{
		_orderManager = orderManager;
		_cancellationToken = cancellationToken;
		_title = workflow.Conversation.MetaData.Title;
		_buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

		_messagesList = new SourceList<MessageViewModel>();

		_messagesList
			.Connect()
			.Bind(out _messages)
			.Subscribe();

		UiContext = uiContext;
		Workflow = workflow;
		OrderNumber = orderNumber;

		HasUnreadMessagesObs = _messagesList.Connect()
											.AutoRefresh(x => x.IsUnread)
											.Filter(x => x.IsUnread is true)
											.Count()
											.Select(i => i > 0);

		CanRemoveObs = this.WhenAnyValue(x => x.Workflow.Conversation.Id)
						   .Select(id => id != ConversationId.Empty);

		RemoveOrderCommand = ReactiveCommand.CreateFromTask(RemoveOrderAsync, CanRemoveObs);

		var hasUserMessages =
			_messagesList.CountChanged.Select(_ => _messagesList.Items.Any(x => x is UserMessageViewModel));

		CanResetObs =
			 this.WhenAnyValue(x => x.Workflow.Conversation.Id)
				 .Select(id => id == ConversationId.Empty)
				 .CombineLatest(hasUserMessages, (a, b) => a && b);

		ResetOrderCommand = ReactiveCommand.CreateFromTask(ResetOrderAsync, CanResetObs);

		// TODO: Remove this once we use newer version of DynamicData
		HasUnreadMessagesObs.BindTo(this, x => x.HasUnreadMessages);

		// TODO
		//this.WhenAnyValue(x => x.IsSelected)
		//	.Where(x => x)
		//	.DoAsync(_ => Workflow.MarkConversationAsRead())
		//	.Subscribe();

		// Update file on disk
		this.WhenAnyValue(x => x.HasUnreadMessages).Where(x => x == false).ToSignal()
			.Merge(_messagesList.Connect().ToSignal())
			.DoAsync(async _ => await UpdateConversationLocallyAsync(cancellationToken))
			.Subscribe();

		this.WhenAnyValue(x => x.Workflow.Conversation)
			.Do(conversation =>
			{
				Title = conversation.MetaData.Title;
				RefreshMessageList(conversation);
			})
			.Subscribe();

		this.WhenAnyValue(x => x.Workflow.CurrentStep.IsBusy)
			.BindTo(this, x => x.IsBusy);

		RxApp.MainThreadScheduler.Schedule(StartWorkflow);
	}

	public Workflow Workflow { get; }

	public ConversationId ConversationId => Workflow.Conversation.Id;

	public IObservable<bool> HasUnreadMessagesObs { get; }

	public IObservable<bool> CanRemoveObs { get; }

	public IObservable<bool> CanResetObs { get; }

	public ReadOnlyObservableCollection<MessageViewModel> Messages => _messages;

	public ICommand RemoveOrderCommand { get; }

	public ICommand ResetOrderCommand { get; }

	public int OrderNumber { get; }

	//private async Task SendAsync(CancellationToken cancellationToken)
	//{
	//	IsBusy = true;

	//	try
	//	{
	//		if (WorkflowManager.CurrentWorkflow.IsCompleted)
	//		{
	//			await SendChatHistoryAsync(GetChatMessages(), cancellationToken);
	//		}
	//	}
	//	catch (Exception exception)
	//	{
	//		await ShowErrorAsync("Error while processing order.");
	//		Logger.LogError($"Error while processing order: {exception}).");
	//	}
	//	finally
	//	{
	//		IsBusy = false;
	//	}
	//}

	private async Task RemoveOrderAsync()
	{
		var confirmed = await UiContext.Navigate().To().ConfirmDeleteOrderDialog(this).GetResultAsync();

		if (confirmed)
		{
			await _orderManager.RemoveOrderAsync(OrderNumber);
		}
	}

	private async Task ShowErrorAsync(string message)
	{
		await UiContext.Navigate().To().ShowErrorDialog(message, "Send Failed", "Wasabi was unable to send your message", NavigationTarget.CompactDialogScreen).GetResultAsync();
	}

	private async Task ResetOrderAsync()
	{
		ClearMessageList();
		Workflow.Reset();
	}

	private void RefreshMessageList(Conversation conversation)
	{
		var messages =
			conversation.ChatMessages
						.Select(CreateMessageViewModel)
						.ToArray();

		_messagesList.Edit(x =>
		{
			x.Clear();
			x.Add(messages);
		});
	}

	private void ClearMessageList() => _messagesList.Edit(x => x.Clear());

	private Task UpdateConversationLocallyAsync(CancellationToken cancellationToken)
	{
		if (ConversationId == ConversationId.Empty)
		{
			return Task.CompletedTask;
		}

		return _buyAnythingManager.UpdateConversationOnlyLocallyAsync(Workflow.Conversation, cancellationToken);
	}

	private MessageViewModel CreateMessageViewModel(ChatMessage message)
	{
		if (message.IsMyMessage)
		{
			return new UserMessageViewModel(Workflow, message)
			{
				UiMessage = message.Text,
				OriginalText = message.Text,
				IsUnread = message.IsUnread
			};
		}

		return
			message.Data switch
			{
				OfferCarrier => new OfferMessageViewModel(message),
				Invoice => new PayNowAssistantMessageViewModel(Workflow.Conversation, message),
				AttachmentLinks => new UrlListMessageViewModel(message, "Download your files:"),
				TrackingCodes => new UrlListMessageViewModel(message, "For shipping updates:"),
				_ => new AssistantMessageViewModel(message)
			};
	}

	/// <summary>
	/// Fire and Forget method to start the workflow, and listen to any exceptions
	/// </summary>
	private async void StartWorkflow()
	{
		try
		{
			await Workflow.ExecuteAsync();
		}
		catch (Exception ex)
		{
			await ShowErrorAsync("Error while processing order.");
			Logger.LogError($"Error while processing order: {ex}).");
		}
	}
}
