using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PaymentInputValidator : InputValidator
{
	public PaymentInputValidator(
		IWorkflowValidator workflowValidator,
		Func<string?> message,
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
		WorkflowValidator.SignalValid(true);

		// TODO: Remove step after implementing backend interaction
		WorkflowValidator.SignalNextStep();
	}

	public override bool OnCompletion()
	{
		return false;
	}
}
