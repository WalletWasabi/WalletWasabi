using System.Text;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public sealed class WorkflowRequestError : WorkflowRequest
{
	public string? Error { get; set; }

	public override string ToMessage()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"Error: {Error}");
		return sb.ToString();
	}
}
