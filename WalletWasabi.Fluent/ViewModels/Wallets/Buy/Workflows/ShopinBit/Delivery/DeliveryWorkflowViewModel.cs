using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class DeliveryWorkflowViewModel : WorkflowViewModel
{
	private readonly DeliveryWorkflowRequest _request;

	public DeliveryWorkflowViewModel(IWorkflowValidator workflowValidator)
	{
		_request = new DeliveryWorkflowRequest();

		var termsOfServiceUrl = "https://shopinbit.com/Information/Terms-Conditions/";

		Steps = new List<WorkflowStepViewModel>
		{
			// Info
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"I can offer you an automatic Green Lambo for delivery to Germany by December 24, 2023, at the cost of 300,000 USD or approximately 10.5 BTC.")),
			// Info
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					$"To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:")),
			// Firstname
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"Your First Name:")),
			new (requiresUserInput: true,
				userInputValidator: new FirstNameInputValidator(
					workflowValidator,
					_request)),
			// Lastname
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"Your Last Name:")),
			new (requiresUserInput: true,
				userInputValidator: new LastNameInputValidator(
					workflowValidator,
					_request)),
			// Streetname
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"Street Name:")),
			new (requiresUserInput: true,
				userInputValidator: new StreetNameInputValidator(
					workflowValidator,
					_request)),
			// Housenumber
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"House Number:")),
			new (requiresUserInput: true,
				userInputValidator: new HouseNumberInputValidator(
					workflowValidator,
					_request)),
			// ZIP/Postalcode
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"ZIP/Postal Code:")),
			new (requiresUserInput: true,
				userInputValidator: new PostalCodeInputValidator(
					workflowValidator,
					_request)),
			// City
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"City:")),
			new (requiresUserInput: true,
				userInputValidator: new CityInputValidator(
					workflowValidator,
					_request)),
			// State
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"State:")),
			new (requiresUserInput: true,
				userInputValidator: new StateInputValidator(
					workflowValidator,
					_request)),
			// Confirm
			new (false,
				new DeliverySummaryInputValidator(
					workflowValidator,
					_request)),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmDeliveryInputValidator(
					workflowValidator)),
			// Accept Terms of service
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					$"To continue please accept Privacy Policy: {termsOfServiceUrl}")),
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
					"Accepted Terms of service.",
					"BUY NOW")),
			// Final
			new (false,
				new NoInputInputValidator(
					workflowValidator,
					"Thank you for the information. Please take a moment to verify the accuracy of the provided data. If any details are incorrect, you can make adjustments using the \"EDIT\" button,if everything is correct, click “PLACE ORDER” and accept Terms and Conditions.")),
			// T&C link
			new(false,
				new NoInputInputValidator(
					workflowValidator,
					"www.termsandconditions.com"))
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
