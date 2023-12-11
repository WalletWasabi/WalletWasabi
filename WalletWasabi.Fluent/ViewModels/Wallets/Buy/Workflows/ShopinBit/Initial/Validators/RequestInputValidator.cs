using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class RequestInputValidator : InputValidator
{
	public RequestInputValidator(
		WorkflowState workflowState)
		: base(workflowState, null, "Type here...", "Request")
	{
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
			return Message;
		}

		return null;
	}
}
