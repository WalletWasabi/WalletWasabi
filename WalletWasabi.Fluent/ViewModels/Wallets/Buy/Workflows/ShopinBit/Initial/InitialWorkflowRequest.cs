using System.Text;
using WalletWasabi.BuyAnything;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed class InitialWorkflowRequest : WorkflowRequest
{
	public BuyAnythingClient.Product? Product { get; set; }

	public Country? Location { get; set; }

	public string? Request { get; set; }

	public bool HasAcceptedPrivacyPolicy { get; set; }

	public override string ToMessage()
	{
		var sb = new StringBuilder();
		if (Product is not null)
		{
			sb.AppendLine($"Product: {ProductHelper.GetDescription(Product.Value)}");
		}
		sb.AppendLine($"Location: {Location?.Name}");
		sb.AppendLine($"Request: {Request}");
		sb.AppendLine($"HasAcceptedPrivacyPolicy: {HasAcceptedPrivacyPolicy}");
		return sb.ToString();
	}
}
