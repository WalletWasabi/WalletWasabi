using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ShopinBitWorkflowManagerViewModel : ReactiveObject, IWorkflowManager
{
	private readonly ConversationId _conversationId;
	private readonly Country[] _countries;
	private readonly IWorkflowValidator _workflowValidator;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private Workflow? _currentWorkflow;

	public ShopinBitWorkflowManagerViewModel(ConversationId conversationId, Country[] countries)
	{
		_conversationId = conversationId;
		_countries = countries;
		_workflowValidator = new WorkflowValidator();
	}

	public IWorkflowValidator WorkflowValidator => _workflowValidator;

	public async Task SendApiRequestAsync(CancellationToken cancellationToken)
	{
		// TODO: Just for testing, remove when api service is implemented.
		await Task.Delay(1000);

		if (_currentWorkflow is null)
		{
			return;
		}

		var request = _currentWorkflow.GetResult();

		if (Services.HostedServices.GetOrDefault<BuyAnythingManager>() is { } buyAnythingManager)
		{
			var message = request.ToMessage();
			var metadata = GetMetadata(request);
			await buyAnythingManager.UpdateConversationAsync(_conversationId, message, metadata, cancellationToken);
		}

		switch (request)
		{
			case DeliveryWorkflowRequest deliveryWorkflowRequest:
			{
				// TODO:
				break;
			}
			case InitialWorkflowRequest initialWorkflowRequest:
			{
				// TODO:
				break;
			}
			case PackageWorkflowRequest packageWorkflowRequest:
			{
				// TODO:
				break;
			}
			case PaymentWorkflowRequest paymentWorkflowRequest:
			{
				// TODO:
				break;
			}
			case SupportChatWorkflowRequest supportChatWorkflowRequest:
			{
				// TODO:
				break;
			}
			case WorkflowRequestError workflowRequestError:
			{
				// TODO:
				break;
			}
			default:
			{
				throw new ArgumentOutOfRangeException(nameof(request));
			}
		}
	}

	private object GetMetadata(WorkflowRequest request)
	{
		// TODO:
		switch (request)
		{
			case DeliveryWorkflowRequest:
				return "Delivery";
			case InitialWorkflowRequest:
				return "Initial";
			case PackageWorkflowRequest:
				return "Package";
			case PaymentWorkflowRequest:
				return "Payment";
			case SupportChatWorkflowRequest:
				return "SupportChat";
			case WorkflowRequestError:
				return "Error";
			default:
				return "Unknown";
		}
	}

	private Workflow? GetWorkflowFromCommand(string? command)
	{
		// TODO: What we do if current workflow matched command or is ongoing?
		/*
		switch (command)
		{
			case "Initial":
				return new InitialWorkflow(_workflowValidator, _countries);
			case "Delivery":
				return new DeliveryWorkflow(_workflowValidator);
			case "Payment":
				return new PaymentWorkflow(_workflowValidator);
			case "Package":
				return new PackageWorkflow(_workflowValidator);
			case "SupportChat":
				return new SupportChatWorkflow(_workflowValidator);
			default:
				return null;
		}
		*/
		switch (command)
		{
			case "Started":
				return new InitialWorkflow(_workflowValidator, _countries);
			case "OfferReceived":
				return new DeliveryWorkflow(_workflowValidator);
			case "PaymentDone":
				return new PaymentWorkflow(_workflowValidator);
			case "PaymentConfirmed":
				return new PaymentWorkflow(_workflowValidator);
			case "OfferAccepted":
				return new DeliveryWorkflow(_workflowValidator);
			case "InvoiceReceived":
				return new SupportChatWorkflow(_workflowValidator);
			default:
				return null;
		}
	}

	public bool SelectNextWorkflow(string? command)
	{
		if (command is not null)
		{
			// TODO: Check if we can cancel current workflow.
			if (_currentWorkflow?.CanCancel() ?? true)
			{
				CurrentWorkflow = GetWorkflowFromCommand(command);
				return true;
			}

			return false;
		}

		switch (_currentWorkflow)
		{
			case null:
			{
				CurrentWorkflow = new InitialWorkflow(_workflowValidator, _countries);
				break;
			}
			case InitialWorkflow:
			{
				// TODO:
				CurrentWorkflow = new DeliveryWorkflow(_workflowValidator);
				break;
			}
			case DeliveryWorkflow:
			{
				// TODO:
				CurrentWorkflow = new PaymentWorkflow(_workflowValidator);
				break;
			}
			case PaymentWorkflow:
			{
				// TODO:
				CurrentWorkflow = new PackageWorkflow(_workflowValidator);
				break;
			}
			case PackageWorkflow:
			{
				// TODO: After receiving package info switch to final workflow with chat support.
				CurrentWorkflow = new SupportChatWorkflow(_workflowValidator);
				break;
			}
			case SupportChatWorkflow:
			{
				// TODO: Order is complete do nothing?
				CurrentWorkflow = new SupportChatWorkflow(_workflowValidator);
				break;
			}
		}

		return true;
	}
}
