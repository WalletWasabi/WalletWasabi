namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class DefaultWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	public DefaultWorkflowInputValidatorViewModel(string? message) : base(message)
	{
	}

	public override bool IsValid(string message)
	{
		// TODO: Validate request.
		return !string.IsNullOrWhiteSpace(message);
	}
}
