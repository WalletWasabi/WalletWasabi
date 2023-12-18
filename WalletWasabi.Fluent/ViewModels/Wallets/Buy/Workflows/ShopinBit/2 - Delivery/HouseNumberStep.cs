using System.Collections.Generic;
using System.Threading;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class HouseNumberStep : TextInputStep
{
	public HouseNumberStep(Conversation conversation, CancellationToken token, bool isEditing = false) : base(conversation, token, isEditing)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "House Number:";
	}

	protected override Conversation PutValue(Conversation conversation, string value) =>
		conversation.UpdateMetadata(m => m with { HouseNumber = value });

	protected override string? RetrieveValue(Conversation conversation) =>
		conversation.MetaData.HouseNumber;
}
