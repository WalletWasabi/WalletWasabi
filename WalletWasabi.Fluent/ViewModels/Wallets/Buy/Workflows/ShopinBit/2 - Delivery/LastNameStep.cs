using System.Collections.Generic;
using System.Threading;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class LastNameStep : TextInputStep
{
	public LastNameStep(Conversation conversation, CancellationToken token) : base(conversation, token)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "Last Name:";
	}

	protected override Conversation PutValue(Conversation conversation, string value) => conversation.UpdateMetadata(m => m with { LastName = value });

	protected override string? RetrieveValue(Conversation conversation) => conversation.MetaData.LastName;
}
