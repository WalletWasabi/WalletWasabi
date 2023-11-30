using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PackageWorkflowViewModel : WorkflowViewModel
{
	private readonly PackageWorkflowRequest _request;

	public PackageWorkflowViewModel(IWorkflowValidator workflowValidator)
	{
		_request = new PackageWorkflowRequest();

		// TODO:
		var trackingUrl = "www.trackmypackage.com/trcknmbr0000001";
		var downloadUrl = "www.invoice.com/lamboincoice";

		Steps = new List<WorkflowStepViewModel>
		{
			// Download
			new (false,
				new PackageWorkflowInputValidatorViewModel(
					workflowValidator,
					"Download your files:")),
			// Download links
			new(false,
				new PackageWorkflowInputValidatorViewModel(
					workflowValidator,
					$"{downloadUrl}")),
			// Shipping
			new(false,
				new PackageWorkflowInputValidatorViewModel(
					workflowValidator,
					"For shipping updates:")),
			// Shipping link
			new(false,
				new PackageWorkflowInputValidatorViewModel(
					workflowValidator,
					$"{trackingUrl}")),
			// Vanish message
			new(false,
				new PackageWorkflowInputValidatorViewModel(
					workflowValidator,
					"This conversation will vanish in 30 days, make sure to save all the important info beforehand.\u00a0")),
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
