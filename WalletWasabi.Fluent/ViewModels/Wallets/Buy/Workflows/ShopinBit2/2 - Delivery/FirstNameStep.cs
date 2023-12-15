using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class FirstNameStep : TextInputStep
{
	public FirstNameStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:";

		yield return "Your First Name:";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { FirstName = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.FirstName;
}
