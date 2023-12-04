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
		true;

	public static Conversation AddSystemChatLine(this Conversation conversation, string message,
		ConversationStatus newStatus) =>
		conversation with
		{
			ChatMessages = conversation.ChatMessages.AddSentMessage(message),
			ConversationStatus = newStatus
		};
}
