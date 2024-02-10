using System.Collections.Generic;
using System.Threading;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class ConfirmTosStep : WorkflowStep<bool>
{
	public const string TermsOfServiceUrl = "https://shopinbit.com/Information/Terms-Conditions/";

	public ConfirmTosStep(Conversation conversation, CancellationToken token) : base(conversation, token)
	{
		Caption = "BUY NOW";

		// TODO @SuperJMN: Explore if there's a better option than passing UiContext.Default here. It breaks testing even more.
		Link = new LinkViewModel(UiContext.Default)
		{
			Link = TermsOfServiceUrl,
			Description = "Terms of service",
			IsClickable = true
		};
	}

	public LinkViewModel Link { get; }

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "Thank you for providing your details. Please double-check them for accuracy. If everything looks good, agree to our Terms and Conditions and click 'BUY NOW' to proceed";
	}

	protected override Conversation PutValue(Conversation conversation, bool value) =>
		conversation.UpdateMetadata(m => m with { TermsAccepted = true });

	protected override bool RetrieveValue(Conversation conversation) =>
		conversation.MetaData.TermsAccepted;

	protected override bool ValidateInitialValue(bool value) => value;

	protected override bool ValidateUserValue(bool value) => value;
}
