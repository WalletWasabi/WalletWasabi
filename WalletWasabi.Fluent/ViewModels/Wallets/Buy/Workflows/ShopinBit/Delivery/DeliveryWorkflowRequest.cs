namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed class DeliveryWorkflowRequest : WorkflowRequest
{
	public string? FirstName { get; set; }

	public string? LastName { get; set; }

	public string? StreetName { get; set; }

	public string? HouseNumber { get; set; }

	public string? PostalCode { get; set; }

	public string? City { get; set; }

	public string? State { get; set; }
}
