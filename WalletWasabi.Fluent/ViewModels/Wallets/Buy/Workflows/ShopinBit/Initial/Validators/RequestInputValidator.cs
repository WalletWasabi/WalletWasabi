using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class RequestInputValidator : InputValidator
{
	private readonly InitialWorkflowRequest _initialWorkflowRequest;

	public RequestInputValidator(
		WorkflowState workflowState,
		InitialWorkflowRequest initialWorkflowRequest)
		: base(workflowState, null, "Type here...", "Request")
	{
		_initialWorkflowRequest = initialWorkflowRequest;

		this.WhenAnyValue(x => x.Message)
			.Subscribe(_ => WorkflowState.SignalValid(IsValid()));
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

			_initialWorkflowRequest.Request = message;

			return message;
		}

		return null;
	}
}
