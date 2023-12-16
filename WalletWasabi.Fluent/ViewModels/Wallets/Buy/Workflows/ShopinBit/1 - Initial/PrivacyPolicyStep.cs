using System.Collections.Generic;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

/// <summary>
/// ShopinBit Step #4: Accept the Privacy Policy
/// </summary>
public class PrivacyPolicyStep : WorkflowStep<bool>
{
	private const string PrivacyPolicyUrl = "https://shopinbit.com/Information/Privacy-Policy/";

	public PrivacyPolicyStep(Conversation conversation) : base(conversation)
	{
		PrivacyPolicyLink = new LinkViewModel
		{
			Link = PrivacyPolicyUrl,
			Description = "Privacy Policy",
			IsClickable = true
		};
	}

	public LinkViewModel PrivacyPolicyLink { get; }

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		// Request received + accept Privacy Policy
		yield return "Please accept our Privacy Policy so we can process your request.";
	}

	protected override Conversation PutValue(Conversation conversation, bool value) =>
		conversation.UpdateMetadata(m => m with { PrivacyPolicyAccepted = value });

	protected override bool RetrieveValue(Conversation conversation) =>
		conversation.MetaData.PrivacyPolicyAccepted;

	// This Step is only valid if the Privacy Policy has effectively been accepted (Value is true)
	protected override bool ValidateInitialValue(bool value) => value;

	// This Step is only valid if the Privacy Policy has effectively been accepted (Value is true)
	protected override bool ValidateUserValue(bool value) => value;

	protected override string? StringValue(bool value) => null;
}
