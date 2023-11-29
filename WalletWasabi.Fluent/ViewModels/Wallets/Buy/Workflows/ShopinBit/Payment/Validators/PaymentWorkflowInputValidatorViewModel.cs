using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PaymentWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	public PaymentWorkflowInputValidatorViewModel(
		IWorkflowValidator workflowValidator,
		string? message,
		string? watermark = null,
		string? content = "...") : base(workflowValidator, message, watermark, content)
	{
	}

	public override bool IsValid()
	{
		return true;
	}

	public override string? GetFinalMessage()
	{
		return null;
	}

	public override void OnActivation()
	{
		WorkflowValidator.Signal(true);

		// TODO: Remove step after implementing backend interaction
		WorkflowValidator.NextStep();
	}
}
