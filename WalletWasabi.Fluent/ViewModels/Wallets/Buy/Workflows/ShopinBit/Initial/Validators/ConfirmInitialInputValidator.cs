namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmInitialInputValidator : InputValidator
{
	public ConfirmInitialInputValidator(
		IWorkflowValidator workflowValidator)
		: base(workflowValidator, null, null, "Confirm")
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
	}
}
