using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class NoInputInputValidator : InputValidator
{
	public NoInputInputValidator(
		WorkflowState workflowState,
		Func<string?> message,
		string? watermark = null,
		string? content = "Request")
		: base(workflowState, message, watermark, content)
	{
	}

	public override bool IsValid()
	{
		return false;
	}

	public override string? GetFinalMessage()
	{
		return null;
	}
}
