using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class DeliveryWorkflow : Workflow
{
	private readonly DeliveryWorkflowRequest _request;

	public DeliveryWorkflow(IWorkflowValidator workflowValidator)
	{
		_request = new DeliveryWorkflowRequest();

		var termsOfServiceUrl = "https://shopinbit.com/Information/Terms-Conditions/";

		Steps = new List<WorkflowStep>
		{
			// Info
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					() => $"To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:")),
			// Firstname
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "Your First Name:")),
			new (requiresUserInput: true,
				userInputValidator: new FirstNameInputValidator(
					workflowValidator,
					_request)),
			// Lastname
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "Your Last Name:")),
			new (requiresUserInput: true,
				userInputValidator: new LastNameInputValidator(
					workflowValidator,
					_request)),
			// Streetname
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "Street Name:")),
			new (requiresUserInput: true,
				userInputValidator: new StreetNameInputValidator(
					workflowValidator,
					_request)),
			// Housenumber
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "House Number:")),
			new (requiresUserInput: true,
				userInputValidator: new HouseNumberInputValidator(
					workflowValidator,
					_request)),
			// ZIP/Postalcode
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "ZIP/Postal Code:")),
			new (requiresUserInput: true,
				userInputValidator: new PostalCodeInputValidator(
					workflowValidator,
					_request)),
			// City
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "City:")),
			new (requiresUserInput: true,
				userInputValidator: new CityInputValidator(
					workflowValidator,
					_request)),
			// State
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "State:")),
			new (requiresUserInput: true,
				userInputValidator: new StateInputValidator(
					workflowValidator,
					_request)),
			// // Confirm
			// new (false,
			// 	new DeliverySummaryInputValidator(
			// 		workflowValidator,
			// 		_request)),
			// new (requiresUserInput: true,
			// 	userInputValidator: new ConfirmDeliveryInputValidator(
			// 		workflowValidator)),
			// Accept Terms of service
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => $"Thank you for providing your details. Please double-check them for accuracy. If everything looks good, agree to our Terms and Conditions and click 'BUY NOW' to proceed")),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmTosInputValidator(
					workflowValidator,
					_request,
					new LinkViewModel
					{
						Link = termsOfServiceUrl,
						Description = "Accept the Terms of service",
						IsClickable = true
					},
					() => null,
					"BUY NOW")),
		};

		CreateCanEditObservable();
	}

	public override WorkflowRequest GetResult() => _request;
}
