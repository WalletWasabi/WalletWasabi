namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmDeliveryWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	public ConfirmDeliveryWorkflowInputValidatorViewModel(
		IWorkflowValidator workflowValidator)
		: base(workflowValidator, null, null)
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
