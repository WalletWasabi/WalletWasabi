using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class LastNameInputValidator : TextInputInputValidator
{
	private readonly DeliveryWorkflowRequest _deliveryWorkflowRequest;

	public LastNameInputValidator(
		IWorkflowValidator workflowValidator,
		DeliveryWorkflowRequest deliveryWorkflowRequest)
		: base(workflowValidator, null, "Type here...")
	{
		_deliveryWorkflowRequest = deliveryWorkflowRequest;

		this.WhenAnyValue(x => x.Message)
			.Subscribe(_ => WorkflowValidator.SignalValid(IsValid()));
	}

	public override bool IsValid()
	{
		// TODO: Validate request.
		return !string.IsNullOrWhiteSpace(Message);
	}

	public override string? GetFinalMessage()
	{
		if (IsValid())
		{
			var message = Message;

			_deliveryWorkflowRequest.LastName = message;

			return message;
		}

		return null;
	}
}
