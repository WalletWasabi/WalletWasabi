using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class LastNameStep : TextInputStep
{
	public LastNameStep(Conversation conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "Your Last Name:";
	}

	protected override Conversation PutValue(Conversation conversation, string value) => conversation.UpdateMetadata(m => m with { LastName = value });

	protected override string? RetrieveValue(Conversation conversation) => conversation.MetaData.LastName;
}
