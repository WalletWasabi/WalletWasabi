using System.Collections.Generic;
using System.Threading;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit._4___Finished;

public class OrderFinishedMessage : WorkflowStep<bool>
{
	public OrderFinishedMessage(Conversation conversation, CancellationToken token, bool isEditing = false) : base(conversation, token, isEditing)
	{
		Value = true;
		if (!RetrieveValue(conversation))
		{
			SetCompleted();
		}
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "I'll be available for the next 30 days to assist with any questions you might have.";
	}

	protected override Conversation PutValue(Conversation conversation, bool value) =>
		conversation.UpdateMetadata(m => m with { OrderFinished = true });

	protected override bool RetrieveValue(Conversation conversation) =>
		conversation.MetaData.OrderFinished;

	protected override bool ValidateInitialValue(bool value) => value;

	protected override bool ValidateUserValue(bool value) => value;
}
