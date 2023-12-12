using System.Collections.Generic;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class InitialWorkflow : Workflow
{
	public InitialWorkflow(WorkflowState workflowState, Country[] countries)
	{
		var privacyPolicyUrl = "https://shopinbit.com/Information/Privacy-Policy/";

		Steps = new List<WorkflowStep>
		{
			// Welcome
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "Welcome to our 'Buy Anything' service! To get started, please select the assistant that best fits your needs.")),
			// Fast Travel Assistant
			new(false,
				new DefaultInputValidator(
					workflowState,
					() => "Fast Travel Assistant\n\nChoose this option if you have a specific flight or hotel in mind and need quick assistance with booking.")),
			// General Travel Assistant
			new(false,
				new DefaultInputValidator(
					workflowState,
					() => "General Travel Assistant\n\nSelect this if you're just starting to plan your travel and don't have any travel details yet.")),
			// All-Purpose Concierge Assistant
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "All-Purpose Concierge Assistant\n\nOur all-purpose assistant, ready to help with a wide range of purchases, from vehicles to tech gadgets and more")),
			// Pick one (dropdown)
			new(requiresUserInput: true,
				userInputValidator: new ProductInputValidator(
					workflowState,
					this,
					ChatMessageMetaData.ChatMessageTag.AssistantType)),
			// Assistant greeting, min order limit
			new(false,
				new DefaultInputValidator(
					workflowState,
					() => $"Hello, I am your chosen {GetAssistantName()}. At present, we focus on requests where the value of the goods or services is at least $1,000 USD")),
			// Location
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "To start, please indicate your country. If your order involves shipping, provide the destination country. For non-shipping orders, please specify your nationality.")),
			new (requiresUserInput: true,
				userInputValidator: new LocationInputValidator(
					workflowState,
					countries,
					ChatMessageMetaData.ChatMessageTag.Country)),
			// What
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "What specific assistance do you need today? Be as precise as possible for faster response.")),
			new (requiresUserInput: true,
				userInputValidator: new RequestInputValidator(
					workflowState)),
			// Send Request
			new (requiresUserInput: true,
				userInputValidator: new ConfirmRequestInputValidator(
					workflowState,
					() => null)),
		};

		CreateCanEditObservable();
	}

	public BuyAnythingClient.Product? Product { get; set; }

	private string GetAssistantName()
	{
		return (Product is not null) switch
		{
			true => ProductHelper.GetDescription(Product.Value),
			_ => "Assistant"
		};
	}
}
