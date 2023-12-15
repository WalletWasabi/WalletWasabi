using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class FirstNameStep : TextInputStep
{
	public FirstNameStep(Conversation conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:";

		yield return "Your First Name:";
	}

	protected override Conversation PutValue(Conversation conversation, string value) =>
		conversation.UpdateMetadata(m => m with { FirstName = value });

	protected override string? RetrieveValue(Conversation conversation) =>
		conversation.MetaData.FirstName;
}
