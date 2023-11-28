using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PaymentWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	public PaymentWorkflowInputValidatorViewModel(
		IWorkflowValidator workflowValidator,
		string? message,
		string? watermark = null) : base(workflowValidator, message, watermark)
	{
	}

	public override bool IsValid()
	{
		// TODO: Validate request.
		return false;
	}

	public override string? GetFinalMessage()
	{
		return null;
	}
}
