using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class DeliveryWorkflowViewModel : WorkflowViewModel
{
	private readonly DeliveryWorkflowRequest _request;

	public DeliveryWorkflowViewModel(IWorkflowValidator workflowValidator, string userName)
	{
		_request = new DeliveryWorkflowRequest();

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
			// Confirm
			new (requiresUserInput: true,
				userInputValidator: new ConfirmDeliveryWorkflowInputValidatorViewModel(
					workflowValidator,
					_request)),
			// Final
			new (false,
				new NoInputWorkflowInputValidatorViewModel(
					workflowValidator,
					"Thank you! I have everything to deliver the product to you."))
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
