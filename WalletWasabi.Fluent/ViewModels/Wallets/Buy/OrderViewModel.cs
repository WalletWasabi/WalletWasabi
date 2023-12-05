using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public partial class OrderViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<MessageViewModel> _messages;
	private readonly SourceList<MessageViewModel> _messagesList;
	private readonly UiContext _uiContext;
	private readonly IWorkflowManager _workflowManager;
	private readonly IOrderManager _orderManager;
	private readonly CancellationToken _cancellationToken;

	[AutoNotify] private bool _isBusy;
	[AutoNotify] private bool _isCompleted;
	[AutoNotify] private bool _hasUnreadMessages;
	[AutoNotify] private MessageViewModel? _selectedMessage;

	public OrderViewModel(UiContext uiContext,
		ConversationId id,
		string title,
		IWorkflowManager workflowManager,
		IOrderManager orderManager,
		CancellationToken cancellationToken)
	{
		Id = id;
		Title = title;

		// TODO: For now we have only one workflow manager.
		_uiContext = uiContext;
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

		_workflowManager.SelectNextWorkflow(null);

		SendCommand = ReactiveCommand.CreateFromTask(SendAsync, _workflowManager.WorkflowValidator.IsValidObservable);

		RemoveOrderCommand = ReactiveCommand.CreateFromTask(RemoveOrderAsync);

		_orderManager.UpdateTrigger.Subscribe(m => UpdateOrder(m.Id, m.Command, m.Messages));

		// TODO: Remove this once we use newer version of DynamicData
		HasUnreadMessagesObs.BindTo(this, x => x.HasUnreadMessages);

		UpdateOrder(id, null, null);

		// TODO: Run initial workflow steps if any.
		// RunNoInputWorkflowSteps();
	}

	public IObservable<bool> HasUnreadMessagesObs { get; }

	public ConversationId Id { get; }

	public string Title { get; }

	public IReadOnlyCollection<MessageViewModel> Messages => _messages;

	public IWorkflowManager WorkflowManager => _workflowManager;

	public ICommand SendCommand { get; }

	public ICommand RemoveOrderCommand { get; }

	private void UpdateOrder(ConversationId id, string? command, IReadOnlyList<MessageViewModel>? messages)
	{
		if (id != Id)
		{
			return;
		}

		IsCompleted = _orderManager.IsCompleted(id);

		// HasUnreadMessages = _orderManager.HasUnreadMessages(id);

		if (messages is not null)
		{
			UpdateMessages(messages);
		}

		if (command is not null)
		{
			SelectNextWorkflow(command);
		}
	}

	private async Task SendAsync(CancellationToken cancellationToken)
	{
		IsBusy = true;

		try
		{
			_workflowManager.WorkflowValidator.Signal(false);

			// TODO: Only for form messages and not api calls.
			await Task.Delay(500);

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
				// TODO: Handle agent command?
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
				if ( nextStep.UserInputValidator.CanDisplayMessage())
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
				RunNoInputWorkflowSteps();
			}

			if (_workflowManager.CurrentWorkflow.IsCompleted)
			{
				// TODO: Send request to api service.
				// await _workflowManager.SendApiRequestAsync(cancellationToken);

				SelectNextWorkflow(null);
			}
		}
		catch (Exception exception)
		{
			// TODO: Add propert error handling.
			AddErrorMessage($"Error: {exception.Message}");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool SelectNextWorkflow(string? command)
	{
		// TODO: Select next workflow or wait for api service response.
		_workflowManager.SelectNextWorkflow(command);

		_workflowManager.WorkflowValidator.Signal(false);

		// TODO: After workflow is completed we either wait for service api message or check if next workflow can be run.
		RunNoInputWorkflowSteps();

		// Continue the loop until next workflow is there and is completed.
		if (_workflowManager.CurrentWorkflow is not null)
		{
			if (_workflowManager.CurrentWorkflow.IsCompleted)
			{
				// TODO: Handle agent command?
				SelectNextWorkflow(null);
			}
		}

		return true;
	}

	private void RunNoInputWorkflowSteps()
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
			if (nextStep is not null)
			{
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
			_orderManager.RemoveOrder(Id);
		}
	}

	public void Update()
	{
		// TODO: For testing
		RunNoInputWorkflowSteps();
	}

	// TODO: Temporary until we sync messages
	public void UpdateMessages(IReadOnlyList<MessageViewModel> messages)
	{
		// TODO: We need to sync with current workflow.
		_messagesList.Edit(x =>
		{
			x.Clear();
			x.Add(messages);
		});
	}
}
