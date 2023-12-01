using System.Text;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class InitialSummaryInputValidator : InputValidator
{
	private readonly InitialWorkflowRequest _initialWorkflowRequest;

	public InitialSummaryInputValidator(
		IWorkflowValidator workflowValidator,
		InitialWorkflowRequest initialWorkflowRequest)
		: base(workflowValidator, null, null, null)
	{
		_initialWorkflowRequest = initialWorkflowRequest;
	}

	public override bool IsValid()
	{
		return true;
	}

	public override string? GetFinalMessage()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"Location: {_initialWorkflowRequest.Location}");
		sb.AppendLine($"Request: {_initialWorkflowRequest.Request}");
		return sb.ToString();
	}
}
