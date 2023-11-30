using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ShopinBitWorkflowManagerViewModel : ReactiveObject, IWorkflowManager
{
	private readonly ConversationId _conversationId;
	private readonly IWorkflowValidator _workflowValidator;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private WorkflowViewModel? _currentWorkflow;

	public ShopinBitWorkflowManagerViewModel(ConversationId conversationId)
	{
		_conversationId = conversationId;
		_workflowValidator = new WorkflowValidatorViewModel();
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

	public void SelectNextWorkflow()
	{
		switch (_currentWorkflow)
		{
			case null:
			{
				CurrentWorkflow = new InitialWorkflowViewModel(_workflowValidator);
				break;
			}
			case InitialWorkflowViewModel:
			{
				// TODO:
				CurrentWorkflow = new DeliveryWorkflowViewModel(_workflowValidator);
				break;
			}
			case DeliveryWorkflowViewModel:
			{
				// TODO:
				CurrentWorkflow = new PaymentWorkflowViewModel(_workflowValidator);
				break;
			}
			case PaymentWorkflowViewModel:
			{
				// TODO:
				CurrentWorkflow = new PackageWorkflowViewModel(_workflowValidator);
				break;
			}
			case PackageWorkflowViewModel:
			{
				// TODO: After receiving package info switch to final workflow with chat support.
				CurrentWorkflow = new SupportChatWorkflowViewModel();
				break;
			}
			case SupportChatWorkflowViewModel:
			{
				// TODO: Order is complete do nothing?
				break;
			}
		}
	}
}
