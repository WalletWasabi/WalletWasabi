using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class PaymentWorkflow : Workflow
{
	private readonly PaymentWorkflowRequest _request;

	public PaymentWorkflow(IWorkflowValidator workflowValidator)
	{
		_request = new PaymentWorkflowRequest();

		// TODO:
		var paymentAmount = "10.5 BTC";
		var paymentAddress = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh";

		Steps = new List<WorkflowStep>
		{
			// Info
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					$"To finalize your order, please send {paymentAmount} to the following address:")),
			// Address
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					$"{paymentAddress}")),
			// Payment
			new (requiresUserInput: false,
				userInputValidator: new DefaultInputValidator(
					workflowValidator,
					"Your payment must confirm within 30 minutes in order to initiate the delivery process.")),
			// TODO: Remove step after implementing backend interaction
			// new (false,
			// 	new PaymentInputValidator(
			// 		workflowValidator,
			// 		"Great news! Your order is complete.")),
			new (false,
				new NoInputInputValidator(
					workflowValidator,
					"Fantastic! Your order has been processed successfully"))
		};

		CreateCanEditObservable();
	}

	public override WorkflowRequest GetResult() => _request;
}
