using System.Text;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed class DeliveryWorkflowRequest : WorkflowRequest
{
	public bool HasAcceptedTermsOfService { get; set; }

	public string? FirstName { get; set; }

	public string? LastName { get; set; }

	public string? StreetName { get; set; }

	public string? HouseNumber { get; set; }

	public string? PostalCode { get; set; }

	public string? City { get; set; }

	public WebClients.ShopWare.Models.State? State { get; set; }

	public override string ToMessage()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"FirstName: {FirstName}");
		sb.AppendLine($"LastName: {LastName}");
		sb.AppendLine($"StreetName: {StreetName}");
		sb.AppendLine($"HouseNumber: {HouseNumber}");
		sb.AppendLine($"PostalCode: {PostalCode}");
		sb.AppendLine($"City: {City}");
		if (State is not null)
		{
			sb.AppendLine($"State: {State?.Name}");
		}
		sb.AppendLine($"HasAcceptedTermsOfService: {HasAcceptedTermsOfService}");
		return sb.ToString();
	}
}
