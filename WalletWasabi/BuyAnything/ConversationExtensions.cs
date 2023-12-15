using Microsoft.VisualBasic;
using System;
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

	public static Conversation AddSystemChatLine(this Conversation conversation, string message, DataCarrier data,
		ConversationStatus newStatus) =>
		conversation with
		{
			ChatMessages = new(conversation.ChatMessages.Append(new ChatMessage(MessageSource.Bot, message, IsUnread: true, null, data))),
			ConversationStatus = newStatus
		};

	public static Conversation AddUserMessage(this Conversation conversation, string msg, string? stepName = null) =>
		conversation with { ChatMessages = new(conversation.ChatMessages.Append(new ChatMessage(MessageSource.User, msg, IsUnread: false, stepName))) };

	public static Conversation AddBotMessage(this Conversation conversation, string msg, DataCarrier? data = null, string? stepName = null) =>
		conversation with { ChatMessages = new(conversation.ChatMessages.Append(new ChatMessage(MessageSource.User, msg, IsUnread: false, stepName))) };

	public static Conversation AddAgentMessage(this Conversation conversation, string msg) =>
		conversation with { ChatMessages = new(conversation.ChatMessages.Append(new ChatMessage(MessageSource.User, msg, IsUnread: false, null))) };

	public static Conversation UpdateMetadata(this Conversation conversation, Func<ConversationMetaData, ConversationMetaData> updateMetadata)
	{
		return conversation with { MetaData = updateMetadata(conversation.MetaData) };
	}
}
