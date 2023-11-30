using System.Text;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed class SupportChatWorkflowRequest : WorkflowRequest
{
	public string? Message { get; set; }

	public override string ToMessage()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"Message: {Message}");
		return sb.ToString();
	}
}
