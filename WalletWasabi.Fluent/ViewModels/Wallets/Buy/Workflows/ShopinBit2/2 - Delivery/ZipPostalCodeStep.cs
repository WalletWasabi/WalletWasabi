using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class ZipPostalCodeStep : TextInputStep
{
	public ZipPostalCodeStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "ZIP/Postal Code:";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { PostalCode = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.PostalCode;
}
