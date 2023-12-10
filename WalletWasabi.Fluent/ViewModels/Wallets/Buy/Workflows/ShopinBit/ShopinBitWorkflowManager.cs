using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ShopinBitWorkflowManagerViewModel : ReactiveObject, IWorkflowManager
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

	public Task UpdateConversationLocallyAsync(ChatMessage[] chatMessages, CancellationToken cancellationToken)
	{
		if (Id == ConversationId.Empty || _currentWorkflow is null || Services.HostedServices.GetOrDefault<BuyAnythingManager>() is not { } buyAnythingManager)
		{
			return Task.CompletedTask;
		}

		return buyAnythingManager.UpdateConversationOnlyLocallyAsync(Id, chatMessages, cancellationToken);
	}

	public Task SendChatHistoryAsync(ChatMessage[] chatMessages, CancellationToken cancellationToken)
	{
		if (Id == ConversationId.Empty || _currentWorkflow is null || Services.HostedServices.GetOrDefault<BuyAnythingManager>() is not { } buyAnythingManager)
		{
			return Task.CompletedTask;
		}

		return buyAnythingManager.UpdateConversationAsync(Id, chatMessages, cancellationToken);
	}

	public async Task SendApiRequestAsync(ChatMessage[] chatMessages, ConversationMetaData metaData, CancellationToken cancellationToken)
	{
		if (_currentWorkflow is null || Services.HostedServices.GetOrDefault<BuyAnythingManager>() is not { } buyAnythingManager)
		{
			return;
		}

		var request = _currentWorkflow.GetResult();

		switch (request)
		{
			case InitialWorkflowRequest initialWorkflowRequest:
				{
					if (initialWorkflowRequest.Location is not { } location ||
						initialWorkflowRequest.Product is not { } product ||
						initialWorkflowRequest.Request is not { } requestMessage) // TODO: Delete, this is redundant, we send out the whole conversation to generate a new order.
					{
						throw new ArgumentException($"Argument was not provided!");
					}

					metaData = metaData with { Country = location };

					await buyAnythingManager.StartNewConversationAsync(
						_walletId,
						location.Id,
						product,
						chatMessages,
						metaData,
						cancellationToken);
					break;
				}
			case DeliveryWorkflowRequest deliveryWorkflowRequest:
				{
					if (deliveryWorkflowRequest.FirstName is not { } firstName ||
						deliveryWorkflowRequest.LastName is not { } lastName ||
						deliveryWorkflowRequest.StreetName is not { } streetName ||
						deliveryWorkflowRequest.HouseNumber is not { } houseNumber ||
						deliveryWorkflowRequest.PostalCode is not { } postalCode ||
						// TODO: deliveryWorkflowRequest.State is not { } state ||
						deliveryWorkflowRequest.City is not { } city ||
						metaData.Country is not { } country
					   )
					{
						throw new ArgumentException($"Argument was not provided!");
					}

					var state = deliveryWorkflowRequest.State;

					await buyAnythingManager.AcceptOfferAsync(
						Id,
						firstName,
						lastName,
						streetName,
						houseNumber,
						postalCode,
						city,
						state is not null ? state.Id : "stateId", // TODO: use state variable, but ID is required, not name.
						country.Id,
						cancellationToken);
					break;
				}
		}
	}

	/// <summary>
	/// Selects next scripted workflow or use conversationStatus to override.
	/// </summary>
	/// <param name="conversationStatus">The remote conversationStatus override to select next workflow.</param>
	/// <param name="states"></param>
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

	public void Update(Action<string> onNewMessage)
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
					onNewMessage(message);
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
