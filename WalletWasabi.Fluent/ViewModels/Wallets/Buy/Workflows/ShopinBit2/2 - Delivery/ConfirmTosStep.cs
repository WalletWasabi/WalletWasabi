using System.Collections.Generic;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class ConfirmTosStep : WorkflowStep2<bool>
{
	public const string TermsOfServiceUrl = "https://shopinbit.com/Information/Terms-Conditions/";

	public ConfirmTosStep(Conversation2 conversation) : base(conversation)
	{
		Caption = "BUY NOW";

		Link = new LinkViewModel
		{
			Link = TermsOfServiceUrl,
			Description = "Accept the Terms of service",
			IsClickable = true
		};
	}

	public LinkViewModel Link { get; }

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "Thank you for providing your details. Please double-check them for accuracy. If everything looks good, agree to our Terms and Conditions and click 'BUY NOW' to proceed";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, bool value) =>
		conversation.UpdateMetadata(m => m with { TermsAccepted = true });

	protected override bool RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.TermsAccepted;

	protected override bool ValidateInitialValue(bool value) => value;

	protected override bool ValidateUserValue(bool value) => value;
}
