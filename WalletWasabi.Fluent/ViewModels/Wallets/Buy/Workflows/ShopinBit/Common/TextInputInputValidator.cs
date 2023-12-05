namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public abstract partial class TextInputInputValidator : InputValidator
{
	protected TextInputInputValidator(
		IWorkflowValidator workflowValidator,
		Func<string?>? messageProvider,
		string? watermark,
		string? content = "Next") : base(workflowValidator, messageProvider, watermark, content)
	{
	}
}
