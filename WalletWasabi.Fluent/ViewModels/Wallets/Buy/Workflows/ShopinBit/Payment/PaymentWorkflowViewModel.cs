using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class PaymentWorkflowViewModel : WorkflowViewModel
{
	private readonly PaymentWorkflowRequest _request;

	public PaymentWorkflowViewModel(IWorkflowValidator workflowValidator)
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
					$"To finalize your order, kindly transfer {paymentAmount} to the following address:")),
			// Address
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					$"{paymentAddress}")),
			// Payment
			new (requiresUserInput: false,
				userInputValidator: new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"Once your payment is confirmed, we'll initiate the delivery process.")),
			// TODO: Remove step after implementing backend interaction
			new (false,
				new PaymentWorkflowInputValidatorViewModel(
					workflowValidator,
					"Great news! Your order is complete."))
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
