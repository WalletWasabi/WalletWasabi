namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed class InitialWorkflowRequest : WorkflowRequest
{
	public string? Location { get; set; }

	public string? Request { get; set; }
}
