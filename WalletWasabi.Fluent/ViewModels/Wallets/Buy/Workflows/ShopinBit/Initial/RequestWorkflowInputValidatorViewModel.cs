using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class RequestWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	public RequestWorkflowInputValidatorViewModel(IWorkflowValidator workflowValidator)
		: base(workflowValidator, null, "Type here...")
	{
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
			return Message;
		}

		return null;
	}
}
