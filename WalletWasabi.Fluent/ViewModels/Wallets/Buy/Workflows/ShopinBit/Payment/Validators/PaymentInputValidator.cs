using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PaymentInputValidator : InputValidator
{
	public PaymentInputValidator(
		WorkflowState workflowState,
		Func<string?> message,
		string? watermark = null,
		string? content = "...") : base(workflowState, message, watermark, content)
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

		// TODO: Remove step after implementing backend interaction
		WorkflowState.SignalNextStep();
	}

	public override bool OnCompletion()
	{
		return false;
	}
}
