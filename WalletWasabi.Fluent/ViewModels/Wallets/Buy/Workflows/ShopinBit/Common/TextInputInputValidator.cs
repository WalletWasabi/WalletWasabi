namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public abstract partial class TextInputInputValidator : InputValidator
{
	protected TextInputInputValidator(
		WorkflowState workflowState,
		Func<string?>? messageProvider,
		string? watermark,
		string? content = "Next") : base(workflowState, messageProvider, watermark, content)
	{
	}
}
