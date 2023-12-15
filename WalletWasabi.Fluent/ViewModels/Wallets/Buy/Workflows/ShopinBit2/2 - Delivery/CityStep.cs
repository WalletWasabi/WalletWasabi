using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class CityStep : TextInputStep
{
	public CityStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation2 conversation)
	{
		yield return "City";
	}

	protected override Conversation2 PutValue(Conversation2 conversation, string value) =>
		conversation.UpdateMetadata(m => m with { City = value });

	protected override string? RetrieveValue(Conversation2 conversation) =>
		conversation.MetaData.City;
}
