using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class StreetNameStep : TextInputStep
{
	public StreetNameStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "Street Name:";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { StreetName = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.StreetName;
}
