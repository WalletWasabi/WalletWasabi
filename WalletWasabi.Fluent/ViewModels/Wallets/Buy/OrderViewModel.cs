using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
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

	private WorkflowViewModel? _currentWorkflow;

	[AutoNotify] private string? _message;

	public OrderViewModel(Guid id)
	{
		Id = id;

		_messagesList = new SourceList<MessageViewModel>();

		_messagesList
			.Connect()
			.Bind(out _messages)
			.Subscribe();

		_currentWorkflow = new InitialWorkflowViewModel("PussyCat89");

		var canSend = this.WhenAnyValue(x => x.Message)
			.Select(x => !string.IsNullOrWhiteSpace(x));

		SendCommand = ReactiveCommand.Create<string>(Send, canSend);

		RunNoInputWorkflowSteps();

		// Demo();
	}

	public Guid Id { get; }

	public required string Title { get; init; }

	public IReadOnlyCollection<MessageViewModel> Messages => _messages;

	public ICommand SendCommand { get; set; }

	private void Send(string message)
	{
		AddUserMessage(message);

		if (_currentWorkflow is not null)
		{
			var nextStep = _currentWorkflow.ExecuteNextStep(message);
			if (nextStep is not null)
			{
				if (nextStep.Message is not null)
				{
					AddAssistantMessage(nextStep.Message);
				}

				if (nextStep.IsCompleted)
				{
					RunNoInputWorkflowSteps();
				}
			}
		}

		Message = "";
	}

	private void AddAssistantMessage(string message)
	{
		_messagesList.Edit(x =>
		{
			x.Add(
				new AssistantMessageViewModel()
				{
					Message = message
				});
		});
	}

	private void AddUserMessage(string message)
	{
		_messagesList.Edit(x =>
		{
			x.Add(
				new UserMessageViewModel
				{
					Message = message
				});
		});
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

			var nextStep = _currentWorkflow.ExecuteNextStep(string.Empty);
			if (nextStep is not null)
			{
				if (nextStep.Message is not null)
				{
					AddAssistantMessage(nextStep.Message);
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
