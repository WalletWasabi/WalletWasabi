namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmInitialInputValidator : InputValidator
{
	public ConfirmInitialInputValidator(
		WorkflowState workflowState)
		: base(workflowState, null, null, "Confirm")
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
		WorkflowState.SignalValid(true);
	}
}
