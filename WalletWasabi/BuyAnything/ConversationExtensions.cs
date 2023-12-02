using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.BuyAnything;

public static class ConversationExtensions
{
	public static bool IsCompleted(this Conversation conversation)
	{
		return conversation.OrderStatus == OrderStatus.Done;
	}

	public static bool IsUpdatable(this Conversation conversation) =>
		conversation.OrderStatus is OrderStatus.Open or OrderStatus.InProgress;

	public static Conversation AddSystemChatLines(this Conversation conversation, IEnumerable<string> messages,
		ConversationStatus newStatus) =>
		conversation with
		{
			ChatMessages = conversation.ChatMessages.AddRangeSentMessages(messages),
			ConversationStatus = newStatus
		};
}
