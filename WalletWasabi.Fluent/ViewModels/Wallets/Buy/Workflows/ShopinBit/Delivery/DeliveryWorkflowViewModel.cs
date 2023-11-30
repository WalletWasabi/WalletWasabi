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
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"I can offer you an automatic Green Lambo for delivery to Germany by December 24, 2023, at the cost of 300,000 USD or approximately 10.5 BTC.")),
			// Info
			new(false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					$"To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:")),
			// Firstname
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"Your First Name:")),
			new (requiresUserInput: true,
				userInputValidator: new FirstNameWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// Lastname
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"Your Last Name:")),
			new (requiresUserInput: true,
				userInputValidator: new LastNameWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// Streetname
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"Street Name:")),
			new (requiresUserInput: true,
				userInputValidator: new StreetNameWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// Housenumber
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"House Number:")),
			new (requiresUserInput: true,
				userInputValidator: new HouseNumberWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// ZIP/Postalcode
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"ZIP/Postal Code:")),
			new (requiresUserInput: true,
				userInputValidator: new PostalCodeWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// City
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"City:")),
			new (requiresUserInput: true,
				userInputValidator: new CityWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// State
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"State:")),
			new (requiresUserInput: true,
				userInputValidator: new StateWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// Confirm
			new (false,
				new DeliverySummaryWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmDeliveryWorkflowInputValidatorViewModel(
					workflowValidator)),
			// Accept Terms of service
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					$"To continue please accept Privacy Policy: {termsOfServiceUrl}")),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmTosWorkflowInputValidatorViewModel(
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
				new NoInputWorkflowInputValidatorViewModel(
					workflowValidator,
					"Thank you for the information. Please take a moment to verify the accuracy of the provided data. If any details are incorrect, you can make adjustments using the \"EDIT\" button,if everything is correct, click “PLACE ORDER” and accept Terms and Conditions.")),
			// T&C link
			new(false,
				new NoInputWorkflowInputValidatorViewModel(
					workflowValidator,
					"www.termsandconditions.com"))
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
