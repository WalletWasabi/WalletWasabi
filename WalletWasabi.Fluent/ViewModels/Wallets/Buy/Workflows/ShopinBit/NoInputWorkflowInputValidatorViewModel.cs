namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class NoInputWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	public NoInputWorkflowInputValidatorViewModel(
		string? message,
		string? watermark = null) : base(message, watermark)
	{
	}

	public override bool IsValid()
	{
		// TODO: Validate request.
		return false;
	}

	public override string? GetFinalMessage()
	{
		return null;
	}
}
