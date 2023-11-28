using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PackageWorkflowViewModel : WorkflowViewModel
{
	private readonly PackageWorkflowRequest _request;

	public PackageWorkflowViewModel(IWorkflowValidator workflowValidator, string userName)
	{
		_request = new PackageWorkflowRequest();

		// TODO:
		var trackingUrl = "www.trackmypackage.com/trcknmbr0000001";

		Steps = new List<WorkflowStepViewModel>
		{
			// Info
			new (false,
				new NoInputWorkflowInputValidatorViewModel(
					workflowValidator,
					$"Here you can track the package: {trackingUrl}")),
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
