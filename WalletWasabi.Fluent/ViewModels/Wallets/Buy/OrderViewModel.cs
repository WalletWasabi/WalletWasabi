using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

public partial class OrderViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<MessageViewModel> _messages;
	private readonly SourceList<MessageViewModel> _messagesList;
	private readonly UiContext _uiContext;
	private readonly ConversationMetaData _metaData;
	private readonly string _conversationStatus;
	private readonly IWorkflowManager _workflowManager;
	private readonly IOrderManager _orderManager;
	private readonly CancellationToken _cancellationToken;

	[AutoNotify] private bool _isBusy;
	[AutoNotify] private bool _isCompleted;
	[AutoNotify] private bool _hasUnreadMessages;
	[AutoNotify] private MessageViewModel? _selectedMessage;

	public OrderViewModel(UiContext uiContext,
		Guid id,
		ConversationMetaData metaData,
		string conversationStatus,
		IWorkflowManager workflowManager,
		IOrderManager orderManager,
		CancellationToken cancellationToken)
	{
		Id = id;
		Title = metaData.Title;

		_uiContext = uiContext;
		_metaData = metaData;
		_conversationStatus = conversationStatus;
		_workflowManager = workflowManager;
		_orderManager = orderManager;
		_cancellationToken = cancellationToken;

		_workflowManager.WorkflowValidator.NextStepObservable.Skip(1).Subscribe(async _ =>
		{
			await SendAsync(_cancellationToken);
		});

		_messagesList = new SourceList<MessageViewModel>();

		_messagesList
			.Connect()
			.Bind(out _messages)
			.Subscribe();

		HasUnreadMessagesObs = _messagesList.Connect().AutoRefresh(x => x.IsUnread).Filter(x => x.IsUnread is true).Count().Select(i => i > 0);

		SendCommand = ReactiveCommand.CreateFromTask(SendAsync, _workflowManager.WorkflowValidator.IsValidObservable);

		var canExecuteRemoveCommand = this.WhenAnyValue(x => x._workflowManager.Id).Select(id => id != ConversationId.Empty);

		RemoveOrderCommand = ReactiveCommand.CreateFromTask(RemoveOrderAsync, canExecuteRemoveCommand);

		// TODO: Remove this once we use newer version of DynamicData
		HasUnreadMessagesObs.BindTo(this, x => x.HasUnreadMessages);
	}

	public IObservable<bool> HasUnreadMessagesObs { get; }

	public ConversationId BackendId => WorkflowManager.Id;

	public string Title { get; }

	public IReadOnlyCollection<MessageViewModel> Messages => _messages;

	public IWorkflowManager WorkflowManager => _workflowManager;

	public ICommand SendCommand { get; }

	public ICommand RemoveOrderCommand { get; }

	public Guid Id { get; }

	// TODO: Fragile as f*ck! Workflow management needs to be rewritten.
	public void StartConversation(string conversationStatus)
	{
		// The conversation is empty so just start from the beginning
		if (conversationStatus == "Started" && !Messages.Any())
		{
			_workflowManager.SelectNextWorkflow(null);
			return;
		}

		if (conversationStatus == "Started")
		{
			_workflowManager.SelectNextWorkflow("Support");
			return;
		}

		_workflowManager.SelectNextWorkflow(conversationStatus);
	}

	public void UpdateOrder(
		ConversationId id,
		string? conversationStatus,
		string? orderStatus,
		IReadOnlyList<MessageViewModel>? messages)
	{
		if (id != BackendId)
		{
			return;
		}

		IsCompleted = orderStatus == "Done";

		// HasUnreadMessages = _orderManager.HasUnreadMessages(id);

		if (messages is not null)
		{
			UpdateMessages(messages);
		}

		if (conversationStatus is not null && _conversationStatus != conversationStatus)
		{
			SelectNextWorkflow(conversationStatus);
		}
	}

	private async Task SendAsync(CancellationToken cancellationToken)
	{
		IsBusy = true;

		try
		{
			_workflowManager.WorkflowValidator.Signal(false);

			if (_workflowManager.CurrentWorkflow is null)
			{
				return;
			}

			if (_workflowManager.CurrentWorkflow.CurrentStep is not null)
			{
				if (!_workflowManager.CurrentWorkflow.CurrentStep.UserInputValidator.OnCompletion())
				{
					return;
				}

				if (_workflowManager.CurrentWorkflow.CurrentStep.UserInputValidator.CanDisplayMessage())
				{
					var message = _workflowManager.CurrentWorkflow.CurrentStep.UserInputValidator.GetFinalMessage();

					if (message is not null)
					{
						AddUserMessage(
							message,
							_workflowManager.CurrentWorkflow.EditStepCommand,
							_workflowManager.CurrentWorkflow.CanEditObservable,
							_workflowManager.CurrentWorkflow.CurrentStep);
					}
				}
			}

			if (_workflowManager.CurrentWorkflow.IsCompleted)
			{
				// TODO: Handle agent conversationStatus?
				SelectNextWorkflow(null);
				return;
			}

			var nextStep = _workflowManager.CurrentWorkflow.ExecuteNextStep();
			if (nextStep is null)
			{
				// TODO: Handle error?
				return;
			}

			if (!nextStep.UserInputValidator.OnCompletion())
			{
				return;
			}

			if (!nextStep.RequiresUserInput)
			{
				if (nextStep.UserInputValidator.CanDisplayMessage())
				{
					var nextMessage = nextStep.UserInputValidator.GetFinalMessage();
					if (nextMessage is not null)
					{
						AddAssistantMessage(nextMessage);
					}
				}
			}

			if (nextStep.IsCompleted)
			{
				Update();
			}

			if (_workflowManager.CurrentWorkflow.IsCompleted)
			{
				var chatMessages = GetChatMessages();
				await _workflowManager.SendApiRequestAsync(chatMessages, _metaData, cancellationToken);
				await WorkflowManager.SendChatHistoryAsync(GetChatMessages(), cancellationToken);

				SelectNextWorkflow(null);
			}
		}
		catch (Exception exception)
		{
			AddErrorMessage($"Error while processing order.");
			Logger.LogError($"Error while processing order: {exception}).");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool SelectNextWorkflow(string? conversationStatus)
	{
		_workflowManager.SelectNextWorkflow(conversationStatus);

		_workflowManager.WorkflowValidator.Signal(false);

		Update();

		// Continue the loop until next workflow is there and is completed.
		if (_workflowManager.CurrentWorkflow is not null)
		{
			if (_workflowManager.CurrentWorkflow.IsCompleted)
			{
				SelectNextWorkflow(null);
			}
		}

		return true;
	}

	public void Update()
	{
		if (_workflowManager.CurrentWorkflow is null)
		{
			return;
		}

		while (true)
		{
			var peekStep = _workflowManager.CurrentWorkflow.PeekNextStep();
			if (peekStep is null)
			{
				break;
			}

			var nextStep = _workflowManager.CurrentWorkflow.ExecuteNextStep();
			if (nextStep is null)
			{
				continue;
			}

			if (nextStep.UserInputValidator.CanDisplayMessage())
			{
				var message = nextStep.UserInputValidator.GetFinalMessage();
				if (message is not null)
				{
					AddAssistantMessage(message);
				}
			}

			if (nextStep.RequiresUserInput)
			{
				break;
			}

			if (!nextStep.UserInputValidator.OnCompletion())
			{
				break;
			}
		}
	}

	private void AddErrorMessage(string message)
	{
		var assistantMessage = new ErrorMessageViewModel(null, null)
		{
			Message = message
		};

		_messagesList.Edit(x =>
		{
			x.Add(assistantMessage);
		});

		SelectedMessage = assistantMessage;
	}

	private void AddAssistantMessage(string message)
	{
		var assistantMessage = new AssistantMessageViewModel(null, null)
		{
			Message = message
		};

		_messagesList.Edit(x =>
		{
			x.Add(assistantMessage);
		});

		SelectedMessage = assistantMessage;
	}

	private void AddUserMessage(
		string message,
		ICommand? editCommand,
		IObservable<bool>? canEditObservable,
		WorkflowStep? workflowStep)
	{
		var userMessage = new UserMessageViewModel(editCommand, canEditObservable, workflowStep)
		{
			Message = message
		};

		_messagesList.Edit(x =>
		{
			x.Add(userMessage);
		});

		SelectedMessage = userMessage;
	}

	private async Task RemoveOrderAsync()
	{
		var confirmed = await _uiContext.Navigate().To().ConfirmDeleteOrderDialog(this).GetResultAsync();

		if (confirmed)
		{
			_orderManager.RemoveOrderAsync(Id);
		}
	}

	public void UpdateMessages(IReadOnlyList<MessageViewModel> messages)
	{
		// TODO: We need to sync with current workflow.
		_messagesList.Edit(x =>
		{
			x.Clear();
			x.Add(messages);
		});
	}

	private ChatMessage[] GetChatMessages()
	{
		return _messages
			.Select(x =>
			{
				var message = x.Message ?? "";

				if (x is AssistantMessageViewModel)
				{
					return new ChatMessage(false, message);
				}

				return new ChatMessage(true, message);
			})
			.ToArray();
	}
}
