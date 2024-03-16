using System.Collections.Generic;
using System.Threading;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class StreetNameStep : TextInputStep
{
	public StreetNameStep(Conversation conversation, CancellationToken token, bool isEditing = false) : base(conversation, token, isEditing)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "Street Name:";
	}

	protected override Conversation PutValue(Conversation conversation, string value) =>
		conversation.UpdateMetadata(m => m with { StreetName = value });

	protected override string? RetrieveValue(Conversation conversation) =>
		conversation.MetaData.StreetName;
}
