namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmInitialWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	public ConfirmInitialWorkflowInputValidatorViewModel(
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
