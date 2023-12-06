using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ShopinBitWorkflowManagerViewModel : ReactiveObject, IWorkflowManager
{
	private readonly Country[] _countries;
	private readonly string _walletId;
	private readonly IWorkflowValidator _workflowValidator;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private Workflow? _currentWorkflow;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private ConversationId _id = ConversationId.Empty;

	public ShopinBitWorkflowManagerViewModel(Country[] countries, string walletId)
	{
		_countries = countries;
		_walletId = walletId;
		_workflowValidator = new WorkflowValidator();
	}

	public IWorkflowValidator WorkflowValidator => _workflowValidator;

	public void UpdateId(ConversationId newId)
	{
		if (Id != ConversationId.Empty)
		{
			throw new InvalidOperationException("ID cannot be modified!");
		}

		Id = newId;
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
						deliveryWorkflowRequest.State is not { } state ||
						deliveryWorkflowRequest.City is not { } city ||
						metaData.Country is not { } country
					   )
					{
						throw new ArgumentException($"Argument was not provided!");
					}
					await buyAnythingManager.AcceptOfferAsync(
						Id,
						firstName,
						lastName,
						streetName,
						houseNumber,
						postalCode,
						city,
						"stateId", // TODO: use state variable, but ID is required, not name.
						country.Id,
						cancellationToken);
					break;
				}
		}
	}

	private Workflow? GetWorkflowFromConversation(string? conversationStatus)
	{
		switch (conversationStatus)
		{
			case "Started":
				return new InitialWorkflow(_workflowValidator, _countries);

			case "OfferReceived":
				return new DeliveryWorkflow(_workflowValidator);

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

	public bool SelectNextWorkflow(string? conversationStatus)
	{
		if (conversationStatus is not null)
		{
			if (_currentWorkflow?.CanCancel() ?? true)
			{
				CurrentWorkflow = GetWorkflowFromConversation(conversationStatus);
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
}
