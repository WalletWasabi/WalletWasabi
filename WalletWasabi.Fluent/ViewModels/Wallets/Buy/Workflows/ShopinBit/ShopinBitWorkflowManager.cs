using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ShopinBitWorkflowManagerViewModel : ReactiveObject
{
	private readonly string _walletId;
	private readonly Country[] _countries;
	private readonly IWorkflowValidator _workflowValidator;
	private readonly BehaviorSubject<bool> _idChangedSubject;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private Workflow? _currentWorkflow;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private ConversationId _id = ConversationId.Empty;

	public ShopinBitWorkflowManagerViewModel(string walletId, Country[] countries)
	{
		_walletId = walletId;
		_countries = countries;
		_workflowValidator = new WorkflowValidator();
		_idChangedSubject = new BehaviorSubject<bool>(false);
		IdChangedObservable = _idChangedSubject.AsObservable();
	}

	public string WalletId => _walletId;

	public IObservable<bool> IdChangedObservable { get; }

	public IWorkflowValidator WorkflowValidator => _workflowValidator;

	public void UpdateId(ConversationId newId)
	{
		if (Id != ConversationId.Empty)
		{
			throw new InvalidOperationException("ID cannot be modified!");
		}

		Id = newId;
		_idChangedSubject.OnNext(true);
	}

	/// <summary>
	/// Selects next scripted workflow or use conversationStatus to override.
	/// </summary>
	/// <param name="conversationStatus">The remote conversationStatus override to select next workflow.</param>
	/// <param name="args"></param>
	/// <returns>True is next workflow selected successfully or current workflow will continue.</returns>
	public bool SelectNextWorkflow(string? conversationStatus, object? args)
	{
		var states = args as WebClients.ShopWare.Models.State[];

		if (conversationStatus is not null)
		{
			if (_currentWorkflow?.CanCancel() ?? true)
			{
				CurrentWorkflow = GetWorkflowFromConversation(conversationStatus, states);
				return true;
			}

			return false;
		}

		CurrentWorkflow = _currentWorkflow switch
		{
			null => new InitialWorkflow(_workflowValidator, _countries),
			InitialWorkflow => new SupportChatWorkflow(_workflowValidator),
			DeliveryWorkflow => new SupportChatWorkflow(_workflowValidator),
			SupportChatWorkflow => new SupportChatWorkflow(_workflowValidator),
			_ => CurrentWorkflow
		};

		return true;
	}

	public bool SelectNextWorkflow(string? conversationStatus, CancellationToken cancellationToken, object? args, Action<string> onAssistantMessage)
	{
		var states = args as WebClients.ShopWare.Models.State[];

		SelectNextWorkflow(conversationStatus, states);
		WorkflowValidator.Signal(false);
		RunNoInputWorkflows(onAssistantMessage);

		// Continue the loop until next workflow is there and is completed.
		if (CurrentWorkflow is not null)
		{
			if (CurrentWorkflow.IsCompleted)
			{
				SelectNextWorkflow(null, cancellationToken);
			}
		}

		return true;
	}

	private Workflow? GetWorkflowFromConversation(string? conversationStatus, WebClients.ShopWare.Models.State[] states)
	{
		switch (conversationStatus)
		{
			case "Started":
				return new InitialWorkflow(_workflowValidator, _countries);

			case "OfferReceived":
				return new DeliveryWorkflow(_workflowValidator, states);

			case "PaymentDone":
				return new SupportChatWorkflow(_workflowValidator);

			case "PaymentConfirmed":
				return new SupportChatWorkflow(_workflowValidator);

			case "OfferAccepted":
				return new SupportChatWorkflow(_workflowValidator);

			case "InvoiceReceived":
				return new SupportChatWorkflow(_workflowValidator);

			case "InvoiceExpired":
				return new SupportChatWorkflow(_workflowValidator);

			case "InvoicePaidAfterExpiration":
				return new SupportChatWorkflow(_workflowValidator);

			case "Shipped":
				return new SupportChatWorkflow(_workflowValidator);

			case "Finished":
				return new SupportChatWorkflow(_workflowValidator);

			case "Support":
				return new SupportChatWorkflow(_workflowValidator);

			default:
				return null;
		}
	}

	public void ResetWorkflow()
	{
		if (_currentWorkflow?.CanCancel() ?? true)
		{
			CurrentWorkflow = null;
		}
	}

	public void RunNoInputWorkflows(Action<string> onAssistantMessage)
	{
		if (CurrentWorkflow is null)
		{
			return;
		}

		while (true)
		{
			var peekStep = CurrentWorkflow.PeekNextStep();
			if (peekStep is null)
			{
				break;
			}

			var nextStep = CurrentWorkflow.ExecuteNextStep();
			if (nextStep is null)
			{
				continue;
			}

			if (nextStep.UserInputValidator.CanDisplayMessage())
			{
				var message = nextStep.UserInputValidator.GetFinalMessage();
				if (message is not null)
				{
					onAssistantMessage(message);
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

	public bool RunInputWorkflows(Action<string> onUserMessage, Action<string> onAssistantMessage, object? args, CancellationToken cancellationToken)
	{
		var states = args as WebClients.ShopWare.Models.State[];

		WorkflowValidator.Signal(false);

		if (CurrentWorkflow is null)
		{
			return false;
		}

		if (CurrentWorkflow.CurrentStep is not null)
		{
			if (!CurrentWorkflow.CurrentStep.UserInputValidator.OnCompletion())
			{
				return false;
			}

			if (CurrentWorkflow.CurrentStep.UserInputValidator.CanDisplayMessage())
			{
				var message = CurrentWorkflow.CurrentStep.UserInputValidator.GetFinalMessage();

				if (message is not null)
				{
					onUserMessage(message);
				}
			}
		}

		if (CurrentWorkflow.IsCompleted)
		{
			// TODO: Handle agent conversationStatus?
			SelectNextWorkflow(null, cancellationToken, states, onAssistantMessage);
			return false;
		}

		var nextStep = CurrentWorkflow.ExecuteNextStep();
		if (nextStep is null)
		{
			// TODO: Handle error?
			return false;
		}

		if (!nextStep.UserInputValidator.OnCompletion())
		{
			return false;
		}

		if (!nextStep.RequiresUserInput)
		{
			if (nextStep.UserInputValidator.CanDisplayMessage())
			{
				var nextMessage = nextStep.UserInputValidator.GetFinalMessage();
				if (nextMessage is not null)
				{
					onAssistantMessage(nextMessage);
				}
			}
		}

		if (nextStep.IsCompleted)
		{
			RunNoInputWorkflows(onAssistantMessage);
		}

		return true;
	}
}
