namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ConfirmRequestInputValidator : InputValidator
{
	public ConfirmRequestInputValidator(
		WorkflowState workflowState,
		Func<string?> message,
		string content = "Send")
		: base(workflowState, message, null, content)
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
		WorkflowState.SignalNextStep();
	}

	public override bool OnCompletion()
	{
		return false;
	}
}
