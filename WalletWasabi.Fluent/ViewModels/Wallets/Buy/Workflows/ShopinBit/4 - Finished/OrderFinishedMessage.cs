using System.Collections.Generic;
using System.Threading;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit._4___Finished;

public class OrderFinishedMessage : WorkflowStep<object>
{
	public OrderFinishedMessage(Conversation conversation, CancellationToken token, bool isEditing = false) : base(conversation, token, isEditing)
	{
		SetCompleted();
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "I'll be available for the next 30 days to assist with any questions you might have.";
	}

	protected override object? RetrieveValue(Conversation conversation) => null;

	protected override Conversation PutValue(Conversation conversation, object value) => conversation;
}
