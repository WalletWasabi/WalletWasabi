using System.Collections.Generic;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.WebClients.BuyAnything;

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
					() => "Welcome to our 'Buy Anything' service! To get started, please select the assistant that best fits your needs.")),
			// Fast Travel Assistant
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					() => "Fast Travel Assistant\n\nChoose this option if you have a specific flight or hotel in mind and need quick assistance with booking.")),
			// General Travel Assistant
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					() => "General Travel Assistant\n\nSelect this if you're just starting to plan your travel and don't have any travel details yet.")),
			// All-Purpose Concierge Assistant
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "All-Purpose Concierge Assistant\n\nOur all-purpose assistant, ready to help with a wide range of purchases, from vehicles to tech gadgets and more")),
			// Pick one (dropdown)
			new(requiresUserInput: true,
				userInputValidator: new ProductInputValidator(
					workflowValidator,
					_request)),
			// Assistant greeting, min order limit
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					() => "Hello, I am your chosen Assistant. At present, we focus on requests where the value of the goods or services is at least $1,000 USD")),
			// Location
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "To start, please indicate your country. If your order involves shipping, provide the destination country. For non-shipping orders, please specify your nationality.")),
			new (requiresUserInput: true,
				userInputValidator: new LocationInputValidator(
					workflowValidator,
					countries,
					_request)),
			// What
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "What specific assistance do you need today? Be as precise as possible for faster response.")),
			new (requiresUserInput: true,
				userInputValidator: new RequestInputValidator(
					workflowValidator,
					_request)),
			// Request received + accept Privacy Policy
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => $"We've received your request. Please accept our Privacy Policy and we’ll get in touch with you within {GetWithinHours()} (Monday to Friday).")),
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
					() => null)),
		};

		CreateCanEditObservable();
	}

	private string GetWithinHours()
	{
		return _request.Product switch
		{
			BuyAnythingClient.Product.ConciergeRequest => "24-48 hours",
			BuyAnythingClient.Product.FastTravelBooking => "24-48 hours",
			BuyAnythingClient.Product.TravelConcierge => "48-72 hours",
			_ => "a few days"
		};
	}

	public override WorkflowRequest GetResult() => _request;
}
