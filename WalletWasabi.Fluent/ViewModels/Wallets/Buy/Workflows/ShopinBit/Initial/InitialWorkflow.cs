using System.Collections.Generic;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class InitialWorkflow : Workflow
{
	private readonly InitialWorkflowRequest _request;

	public InitialWorkflow(IWorkflowValidator workflowValidator, Country[] countries)
	{
		_request = new InitialWorkflowRequest();

		var privacyPolicyUrl = "https://shopinbit.com/Information/Privacy-Policy/";

		Steps = new List<WorkflowStep>
		{
			// Welcome
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"I'm here to assist you with anything you need to buy. Whether it's flights, cars, or any other request, just let me know, and I'll take care of it for you.")),
			// Minimum limit
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					"I'd like to kindly inform you that our minimum transaction amount is $1,000 USD. Please feel free to share any requests above this amount")),
			// Product
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"Please select product category.")),
			new (requiresUserInput: true,
				userInputValidator: new ProductInputValidator(
					workflowValidator,
					_request)),
			// Location
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"Let's begin by selecting your country.")),
			new (requiresUserInput: true,
				userInputValidator: new LocationInputValidator(
					workflowValidator,
					countries,
					_request)),
			// What
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					"What would you like to buy?")),
			new (requiresUserInput: true,
				userInputValidator: new RequestInputValidator(
					workflowValidator,
					_request)),
			// Accept Privacy Policy
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					$"to continue please accept Privacy Policy: {privacyPolicyUrl}")),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmPrivacyPolicyInputValidator(
					workflowValidator,
					_request,
					new LinkViewModel
					{
						Link = privacyPolicyUrl,
						Description = "Accept the Privacy Policy",
						IsClickable = true
					},
					"Accepted Privacy Policy.")),
			// Confirm
			new (false,
				new InitialSummaryInputValidator(
					workflowValidator,
					_request)),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmInitialInputValidator(
					workflowValidator)),
			// Final
			new (false,
				new NoInputInputValidator(
					workflowValidator,
					"We've received your request, we will be in touch with you within the next couple of days."))
		};

		CreateCanEditObservable();
	}

	public override WorkflowRequest GetResult() => _request;
}
