using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
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
	PaymentDone,
	PaymentConfirmed,
	OfferAccepted,
	InvoiceReceived
}
public record ConversationId(string WalletId, string EmailAddress, string Password, string OrderId)
{
	public static readonly ConversationId Empty = new("", "", "", "");
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
		new (this.Append(new ChatMessage(true, msg)));

	public Chat AddReceivedMessage(string msg) =>
		new (this.Append(new ChatMessage(false, msg)));

	public Chat AddRangeSentMessages(IEnumerable<string> msgs) =>
		new(this.Concat(msgs.Select(x => new ChatMessage(true, x))));

	public IEnumerator<ChatMessage> GetEnumerator() =>
		Enumerable.AsEnumerable(_messages).GetEnumerator() ;

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();

	public int Count => _messages.Length;

	public static Chat FromText(string text)
	{
		var messages = text.Split("||", StringSplitOptions.RemoveEmptyEntries);

		var chatEntries = new List<ChatMessage>();
		foreach (var message in messages)
		{
			var items = message.Split("#", StringSplitOptions.RemoveEmptyEntries);

			if (items.Length != 2)
			{
				break;
			}

			var isMine = items[0] == "WASABI";
			var chatLine = EnsureProperRawMessage(items[1]);
			chatEntries.Add(new ChatMessage(isMine, chatLine));
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


	// Makes sure that the raw message doesn't contain characters that are used in the protocol. These chars are '#' and '||'.
	private static string EnsureProperRawMessage(string message)
	{
		message = message.Replace("||", " ");
		message = message.Replace('#', '-');
		return message;
	}

}

public record Conversation(ConversationId Id, Chat ChatMessages, OrderStatus OrderStatus, ConversationStatus ConversationStatus, object Metadata);
