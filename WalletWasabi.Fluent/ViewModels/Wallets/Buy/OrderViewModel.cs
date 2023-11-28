using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public partial class OrderViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<MessageViewModel> _messages;
	private readonly SourceList<MessageViewModel> _messagesList;
	private readonly IWorkflowValidator _workflowValidator;

	[AutoNotify] private WorkflowViewModel? _currentWorkflow;
	[AutoNotify] private MessageViewModel? _selectedMessage;

	public OrderViewModel(Guid id)
	{
		Id = id;

		_workflowValidator = new WorkflowValidatorViewModel();

		_messagesList = new SourceList<MessageViewModel>();

		_messagesList
			.Connect()
			.Bind(out _messages)
			.Subscribe();

		_currentWorkflow = new InitialWorkflowViewModel(_workflowValidator, "PussyCat89");

		SendCommand = ReactiveCommand.CreateFromTask(SendAsync, _workflowValidator.IsValidObservable);

		RunNoInputWorkflowSteps();

		// Demo();
	}

	public Guid Id { get; }

	public required string Title { get; init; }

	public IReadOnlyCollection<MessageViewModel> Messages => _messages;

	public ICommand SendCommand { get; set; }

	private async Task SendAsync()
	{
		_workflowValidator.Signal(false);

		// TODO: Only for form messages and not api calls.
		await Task.Delay(200);

		if (_currentWorkflow?.CurrentStep is null)
		{
			return;
		}

		var message = _currentWorkflow.CurrentStep.UserInputValidator.GetFinalMessage();
		if (message is null)
		{
			return;
		}

		AddUserMessage(message);

		var nextStep = _currentWorkflow.ExecuteNextStep();
		if (nextStep is not null)
		{
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
		}
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

	private void RunNoInputWorkflowSteps()
	{
		if (_currentWorkflow is null)
		{
			return;
		}

		while (true)
		{
			var peekStep = _currentWorkflow.PeekNextStep();
			if (peekStep is null)
			{
				break;
			}

			var nextStep = _currentWorkflow.ExecuteNextStep();
			if (nextStep is not null)
			{
				if (nextStep.UserInputValidator.Message is not null)
				{
					AddAssistantMessage(nextStep.UserInputValidator.Message);
				}

				if (nextStep.RequiresUserInput)
				{
					break;
				}
			}
		}
	}

	private void Demo()
	{
		_messagesList.Edit(x =>
		{
			x.AddRange(
				new MessageViewModel[]
				{
					new UserMessageViewModel
					{
						Message = "I want my Lambo ASAP"
					},
					new AssistantMessageViewModel
					{
						Message = "OK, which color do you like it more?"
					},
					new UserMessageViewModel
					{
						Message = "Wasabi colors is right"
					},
					new AssistantMessageViewModel
					{
						Message = "Cool. Your Lamborguini Aventador is about to arrive. Be ready to open your garage's door."
					}
				});
		});
	}
}
