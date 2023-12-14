using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using WalletWasabi.WebClients.BuyAnything;
using Enumerable = System.Linq.Enumerable;

namespace WalletWasabi.BuyAnything;

public enum OrderStatus
{
	Open,
	Done,
	Cancelled,
	InProgress,
};

public enum ConversationStatus
{
	Started,
	OfferReceived,
	PaymentConfirmed,
	OfferAccepted,
	InvoiceReceived,
	InvoiceExpired,
	InvoicePaidAfterExpiration,
	Shipped,
	Finished,
	WaitingForInvoice,
	Deleted
}

public record ConversationId(string WalletId, string EmailAddress, string Password, string OrderId)
{
	public static ConversationId Empty { get; } = new("", "", "", "");
}

public record Chat : IReadOnlyCollection<ChatMessage>
{
	public static readonly Chat Empty = new(Array.Empty<ChatMessage>());
	[JsonConstructor]
	public Chat(IEnumerable<ChatMessage> messages)
	{
		_messages = Enumerable.ToArray(messages);
	}

	private readonly ChatMessage[] _messages;
	public ChatMessage this[int i] => _messages[i];

	public Chat AddSentMessage(string msg) =>
		new(this.Append(new ChatMessage(true, msg, IsUnread: false, ChatMessageMetaData.Empty)));

	public Chat AddReceivedMessage(string msg, DataCarrier data) =>
		new(this.Append(new SystemChatMessage(msg, data, IsUnread: true, ChatMessageMetaData.Empty)));

	public IEnumerator<ChatMessage> GetEnumerator() =>
		Enumerable.AsEnumerable(_messages).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();

	public int Count => _messages.Length;

	public static Chat FromText(string updatedConversation, Chat oldConversation)
	{
		var messages = updatedConversation.Split("||", StringSplitOptions.RemoveEmptyEntries);

		var chatEntries = new List<ChatMessage>();
		foreach (var message in messages)
		{
			var items = message.Split("#", StringSplitOptions.RemoveEmptyEntries);

			if (items.Length != 2)
			{
				break;
			}

			bool isMine = items[0] == "WASABI";
			string chatLine = EnsureProperRawMessage(items[1]);
			bool isUnread = !oldConversation.Contains(chatLine, isMine);
			var metaData = oldConversation.FirstOrDefault(x => x.Message == chatLine)?.MetaData ?? ChatMessageMetaData.Empty;
			chatEntries.Add(new ChatMessage(isMine, chatLine, isUnread, metaData));
		}

		var systemMessages = oldConversation.Where(line => line is SystemChatMessage);
		foreach (var message in systemMessages)
		{
			int index = oldConversation.ToList().IndexOf(message);
			chatEntries[index] = message;
		}

		return new Chat(chatEntries);
	}

	public string ToText()
	{
		StringBuilder result = new();

		foreach (var chatMessage in this)
		{
			var prefix = chatMessage.IsMyMessage ? "WASABI" : "SIB";
			result.Append($"||#{prefix}#{EnsureProperRawMessage(chatMessage.Message)}");
		}

		result.Append("||");

		return result.ToString();
	}

	public bool Contains(string singleMessage, bool isMine)
	{
		foreach (var chatMessage in this)
		{
			if (chatMessage.IsMyMessage == isMine && chatMessage.Message == singleMessage)
			{
				return true;
			}
		}

		return false;
	}

	// Makes sure that the raw message doesn't contain characters that are used in the protocol. These chars are '#' and '||'.
	private static string EnsureProperRawMessage(string message)
	{
		message = message.Replace("||", " ");
		message = message.Replace('#', '-');
		return message;
	}
}

public record Chat2 : IReadOnlyCollection<ChatMessage2>
{
	public static readonly Chat2 Empty = new(Array.Empty<ChatMessage2>());
	[JsonConstructor]
	public Chat2(IEnumerable<ChatMessage2> messages)
	{
		_messages = Enumerable.ToArray(messages);
	}

	private readonly ChatMessage2[] _messages;
	public ChatMessage2 this[int i] => _messages[i];

	public Chat2 AddBotMessage(string msg, string stepName) =>
		new(this.Append(new ChatMessage2(MessageSource.Bot, msg, IsUnread: true, stepName)));

	public Chat2 AddUserMessage(string msg, string stepName) =>
		new(this.Append(new ChatMessage2(MessageSource.User, msg, IsUnread: false, stepName)));

	//public Chat2 AddSentMessage(string msg) =>
	//	new(this.Append(new ChatMessage2(MessageSource.User, msg, IsUnread: false, DataCarrier.NoData)));

	//public Chat2 AddReceivedMessage(string msg, DataCarrier data) =>
	//	new(this.Append(new ChatMessage2(MessageSource.Agent, msg, IsUnread: true, data)));

	public IEnumerator<ChatMessage2> GetEnumerator() =>
		Enumerable.AsEnumerable(_messages).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();

	public int Count => _messages.Length;

	public static Chat2 FromText(string updatedConversation, Chat2 oldConversation)
	{
		var messages = updatedConversation.Split("||", StringSplitOptions.RemoveEmptyEntries);

		var chatEntries = new List<ChatMessage2>();
		foreach (var message in messages)
		{
			var items = message.Split("#", StringSplitOptions.RemoveEmptyEntries);

			if (items.Length != 2)
			{
				break;
			}

			bool isMine = items[0] == "WASABI";
			string chatLine = EnsureProperRawMessage(items[1]);
			bool isUnread = !oldConversation.Contains(chatLine, isMine);
			var metaData = oldConversation.FirstOrDefault(x => x.Message == chatLine)?.MetaData ?? ChatMessageMetaData.Empty;
			chatEntries.Add(new ChatMessage2(isMine, chatLine, isUnread, metaData));
		}

		return new Chat2(chatEntries);
	}

	public string ToText()
	{
		StringBuilder result = new();

		foreach (var chatMessage in this)
		{
			var prefix = chatMessage.IsMyMessage ? "WASABI" : "SIB";
			result.Append($"||#{prefix}#{EnsureProperRawMessage(chatMessage.Message)}");
		}

		result.Append("||");

		return result.ToString();
	}

	public bool Contains(string singleMessage, bool isMine)
	{
		foreach (var chatMessage in this)
		{
			if (chatMessage.IsMyMessage == isMine && chatMessage.Message == singleMessage)
			{
				return true;
			}
		}

		return false;
	}

	// Makes sure that the raw message doesn't contain characters that are used in the protocol. These chars are '#' and '||'.
	private static string EnsureProperRawMessage(string message)
	{
		message = message.Replace("||", " ");
		message = message.Replace('#', '-');
		return message;
	}
}

public record Conversation(ConversationId Id, Chat ChatMessages, OrderStatus OrderStatus, ConversationStatus ConversationStatus, ConversationMetaData MetaData);

public record ConversationMetaData(string Title, BuyAnythingClient.Product? Product = null);

public record Conversation2(ConversationId Id, Chat2 ChatMessages, OrderStatus OrderStatus, ConversationStatus ConversationStatus, ConversationMetaData2 MetaData);

public record ConversationMetaData2(
	string Title,
	BuyAnythingClient.Product? Product = null,
	Country? Country = null,
	string? RequestedItem = null,
	bool PrivacyPolicyAccepted = false);
