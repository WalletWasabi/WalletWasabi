using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PaymentWorkflowViewModel : WorkflowViewModel
{
	private readonly PaymentWorkflowRequest _request;

	public PaymentWorkflowViewModel(IWorkflowValidator workflowValidator, string userName)
	{
		_request = new PaymentWorkflowRequest();

		// TODO:
		var paymentAmount = "10.5 BTC";
		var paymentAddress = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh";

		Steps = new List<WorkflowStepViewModel>
		{
			// Info
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					$"In order to finalize the order please send {paymentAmount} to the following address:")),
			// Address
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					$"{paymentAddress}")),
			// Payment
			new (requiresUserInput: false,
				userInputValidator: new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"Awaiting payment...")),
			// TODO: Remove step after implementing backend interaction
			new (false,
				new PaymentWorkflowInputValidatorViewModel(
					workflowValidator,
					"We have received you payment! Delivery is in progress."))
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
