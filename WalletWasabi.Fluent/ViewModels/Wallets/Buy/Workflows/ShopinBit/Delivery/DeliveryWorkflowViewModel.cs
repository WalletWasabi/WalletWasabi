using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class DeliveryWorkflowViewModel : WorkflowViewModel
{
	private readonly DeliveryWorkflowRequest _request;

	public DeliveryWorkflowViewModel(IWorkflowValidator workflowValidator, string userName)
	{
		_request = new DeliveryWorkflowRequest();

		var termsOfServiceUrl = "https://shopinbit.com/Information/Terms-Conditions/";

		Steps = new List<WorkflowStepViewModel>
		{
			// Info
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					$"Dear {userName}, I can deliver a Green Lambo to Germany the latest by 2023.12.24. for 300,000 USD which is approx 10.5 BTC.")),
			// Firstname
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"What is your Firstname?")),
			new (requiresUserInput: true,
				userInputValidator: new FirstNameWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// Lastname
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"What is your Lastname?")),
			new (requiresUserInput: true,
				userInputValidator: new LastNameWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// Streetname
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"Streetname?")),
			new (requiresUserInput: true,
				userInputValidator: new StreetNameWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// Housenumber
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"Housenumber?")),
			new (requiresUserInput: true,
				userInputValidator: new HouseNumberWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// ZIP/Postalcode
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"ZIP/Postalcode?")),
			new (requiresUserInput: true,
				userInputValidator: new PostalCodeWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// City
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"City?")),
			new (requiresUserInput: true,
				userInputValidator: new CityWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// State
			new (false,
				new DefaultWorkflowInputValidatorViewModel(
					workflowValidator,
					"State?")),
			new (requiresUserInput: true,
				userInputValidator: new StateWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
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
					"Accepted Terms of service.")),
			// Confirm
			new (false,
				new DeliverySummaryWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmDeliveryWorkflowInputValidatorViewModel(
					workflowValidator)),
			// Final
			new (false,
				new NoInputWorkflowInputValidatorViewModel(
					workflowValidator,
					"Thank you! I have everything to deliver the product to you."))
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
