using System.Collections.Generic;
using System.Threading;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class ZipPostalCodeStep : TextInputStep
{
	public ZipPostalCodeStep(Conversation conversation, CancellationToken token) : base(conversation, token)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "ZIP/Postal Code:";
	}

	protected override Conversation PutValue(Conversation conversation, string value) =>
		conversation.UpdateMetadata(m => m with { PostalCode = value });

	protected override string? RetrieveValue(Conversation conversation) =>
		conversation.MetaData.PostalCode;
}
