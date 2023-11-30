using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public partial class OrderViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<MessageViewModel> _messages;
	private readonly SourceList<MessageViewModel> _messagesList;
	private readonly UiContext _uiContext;
	private readonly IWorkflowManager _workflowManager;
	private readonly IOrderManager _orderManager;

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

		_workflowManager.WorkflowValidator.NextStepObservable.Skip(1).Subscribe(async _ =>
		{
			await SendAsync(cancellationToken);
		});

		_messagesList = new SourceList<MessageViewModel>();

		_messagesList
			.Connect()
			.Bind(out _messages)
			.Subscribe();

		_workflowManager.SelectNextWorkflow();

		SendCommand = ReactiveCommand.CreateFromTask(SendAsync, _workflowManager.WorkflowValidator.IsValidObservable);

		RemoveOrderCommand = ReactiveCommand.CreateFromTask(RemoveOrderAsync);

		_orderManager.UpdateTrigger.Subscribe(_=> UpdateOrder());

		UpdateOrder();

		// TODO: Run initial workflow steps if any.
		// RunNoInputWorkflowSteps();
	}

	public ConversationId Id { get; }

	public string Title { get; }

	public IReadOnlyCollection<MessageViewModel> Messages => _messages;

	public IWorkflowManager WorkflowManager => _workflowManager;

	public ICommand SendCommand { get; }

	public ICommand RemoveOrderCommand { get; }

	private void UpdateOrder()
	{
		IsCompleted = _orderManager.IsCompleted(Id);
		HasUnreadMessages = _orderManager.HasUnreadMessages(Id);
		// TODO: Update messages etc.
	}

	private async Task SendAsync(CancellationToken cancellationToken)
	{
		IsBusy = true;

		try
		{
			_workflowManager.WorkflowValidator.Signal(false);

			// TODO: Only for form messages and not api calls.
			await Task.Delay(500);

			if (_workflowManager.CurrentWorkflow?.CurrentStep is null)
			{
				return;
			}

			var message = _workflowManager.CurrentWorkflow.CurrentStep.UserInputValidator.GetFinalMessage();

			if (message is not null)
			{
				AddUserMessage(message);
			}

			if (_workflowManager.CurrentWorkflow.IsCompleted)
			{
				SelectNextWorkflow();
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
				var nextMessage = nextStep.UserInputValidator.GetFinalMessage();
				if (nextMessage is not null)
				{
					AddAssistantMessage(nextMessage);
				}
			}

			if (nextStep.IsCompleted)
			{
				RunNoInputWorkflowSteps();
			}

			if (_workflowManager.CurrentWorkflow.IsCompleted)
			{
				// TODO: Send request to api service.
				await _workflowManager.SendApiRequestAsync(cancellationToken);

				SelectNextWorkflow();
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

	private void SelectNextWorkflow()
	{
		// TODO: Select next workflow or wait for api service response.
		_workflowManager.SelectNextWorkflow();

		_workflowManager.WorkflowValidator.Signal(false);

		// TODO: After workflow is completed we either wait for service api message or check if next workflow can be run.
		RunNoInputWorkflowSteps();
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
				var message = nextStep.UserInputValidator.GetFinalMessage();
				if (message is not null)
				{
					AddAssistantMessage(message);
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
		var assistantMessage = new ErrorMessageViewModel
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
		var assistantMessage = new AssistantMessageViewModel
		{
			Message = message
		};

		_messagesList.Edit(x =>
		{
			x.Add(assistantMessage);
		});

		SelectedMessage = assistantMessage;
	}

	private void AddUserMessage(string message)
	{
		var userMessage = new UserMessageViewModel
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
		var confirmed = await _uiContext.Navigate().To().ConfirmDeleteOrder(this).GetResultAsync();

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
