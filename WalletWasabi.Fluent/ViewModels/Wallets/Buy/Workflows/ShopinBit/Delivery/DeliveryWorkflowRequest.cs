using System.Text;

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

	public override string ToMessage()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"FirstName: {FirstName}");
		sb.AppendLine($"LastName: {LastName}");
		sb.AppendLine($"StreetName: {StreetName}");
		sb.AppendLine($"HouseNumber: {HouseNumber}");
		sb.AppendLine($"PostalCode: {PostalCode}");
		sb.AppendLine($"City: {City}");
		sb.AppendLine($"State: {State}");
		return sb.ToString();
	}
}
