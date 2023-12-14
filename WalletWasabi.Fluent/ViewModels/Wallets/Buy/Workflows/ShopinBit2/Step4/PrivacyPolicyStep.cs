using System.Collections.Generic;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

/// <summary>
/// ShopinBit Step #4: Accept the Privacy Policy
/// </summary>
public class PrivacyPolicyStep : WorkflowStep2<bool>
{
	private const string PrivacyPolicyUrl = "https://shopinbit.com/Information/Privacy-Policy/";

	public PrivacyPolicyStep(Conversation2 conversation) : base(conversation)
	{
		PrivacyPolicyLink = new LinkViewModel
		{
			Link = PrivacyPolicyUrl,
			Description = "Privacy Policy",
			IsClickable = true
		};
	}

	public LinkViewModel PrivacyPolicyLink { get; }

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		// Request received + accept Privacy Policy
		yield return "Please accept our Privacy Policy.";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, bool value) =>
		conversation.UpdateMetadata(m => m with { PrivacyPolicyAccepted = value });

	protected override bool RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.PrivacyPolicyAccepted;

	// This Step is only valid if the Privacy Policy has effectively been accepted (Value is true)
	protected override bool ValidateUserValue(bool value) => value;

	protected override string? StringValue(bool value) => null;
}
