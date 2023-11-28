namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class LocationWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	public LocationWorkflowInputValidatorViewModel() : base(null)
	{
	}

	public override bool IsValid(string message)
	{
		// TODO: Validate location.
		return !string.IsNullOrWhiteSpace(message);
	}
}
