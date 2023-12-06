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
					() => "Please select the assistant that best fits your needs:")),
			// All-Purpose Concierge Assistant
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "All-Purpose Concierge Assistant\n\nSelect this for a wide range of purchases, from vehicles to tech gadgets and more.")),
			// Fast Travel Assistant
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					() => "Fast Travel Assistant\n\nSelect this if you've a specific flight or hotel in mind and need quick assistance with booking.")),
			// General Travel Assistant
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					() => "General Travel Assistant\n\nSelect this if you're just starting to plan your travel and don't have any details yet.")),
			// Pick one (dropdown)
			new(requiresUserInput: true,
				userInputValidator: new ProductInputValidator(
					workflowValidator,
					_request)),
			// Assistant greeting, min order limit
			new(false,
				new DefaultInputValidator(
					workflowValidator,
					() => "At present, we only accept requests for goods or services of at least $1,000 USD.")),
			// Location
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "If your order involves shipping, provide the destination country. For non-shipping orders, specify your nationality.")),
			new (requiresUserInput: true,
				userInputValidator: new LocationInputValidator(
					workflowValidator,
					countries,
					_request)),
			// What
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => "What do you exactly need? Be as precise as possible for faster response.")),
			new (requiresUserInput: true,
				userInputValidator: new RequestInputValidator(
					workflowValidator,
					_request)),
			// Request received + accept Privacy Policy
			new (false,
				new DefaultInputValidator(
					workflowValidator,
					() => $"We've received your request. Please accept our Privacy Policy and we'll get in touch with you within {GetWithinHours()} (Monday to Friday).")),
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
