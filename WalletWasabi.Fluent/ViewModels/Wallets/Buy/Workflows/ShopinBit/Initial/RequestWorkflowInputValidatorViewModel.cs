using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class RequestWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	private readonly InitialWorkflowRequest _initialWorkflowRequest;

	public RequestWorkflowInputValidatorViewModel(
		IWorkflowValidator workflowValidator,
		InitialWorkflowRequest initialWorkflowRequest)
		: base(workflowValidator, null, "Type here...")
	{
		_initialWorkflowRequest = initialWorkflowRequest;

		this.WhenAnyValue(x => x.Message)
			.Subscribe(_ => WorkflowValidator.Signal(IsValid()));
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
