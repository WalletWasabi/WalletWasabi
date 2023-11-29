using System.Text;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class DeliverySummaryWorkflowInputValidatorViewModel : WorkflowInputValidatorViewModel
{
	private readonly DeliveryWorkflowRequest _deliveryWorkflowRequest;

	public DeliverySummaryWorkflowInputValidatorViewModel(
		IWorkflowValidator workflowValidator,
		DeliveryWorkflowRequest deliveryWorkflowRequest)
		: base(workflowValidator, null, null)
	{
		_deliveryWorkflowRequest = deliveryWorkflowRequest;
	}

	public override bool IsValid()
	{
		return true;
	}

	public override string? GetFinalMessage()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"FirstName: {_deliveryWorkflowRequest.FirstName}");
		sb.AppendLine($"LastName: {_deliveryWorkflowRequest.LastName}");
		sb.AppendLine($"StreetName: {_deliveryWorkflowRequest.StreetName}");
		sb.AppendLine($"HouseNumber: {_deliveryWorkflowRequest.HouseNumber}");
		sb.AppendLine($"PostalCode: {_deliveryWorkflowRequest.PostalCode}");
		sb.AppendLine($"City: {_deliveryWorkflowRequest.City}");
		sb.AppendLine($"State: {_deliveryWorkflowRequest.State}");
		return sb.ToString();
	}
}
