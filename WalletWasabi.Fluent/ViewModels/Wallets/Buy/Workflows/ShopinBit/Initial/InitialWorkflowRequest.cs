using System.Text;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed class InitialWorkflowRequest : WorkflowRequest
{
	public string? Location { get; set; }

	public string? Request { get; set; }

	public override string ToMessage()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"Location: {Location}");
		sb.AppendLine($"Request: {Request}");
		return sb.ToString();
	}
}
