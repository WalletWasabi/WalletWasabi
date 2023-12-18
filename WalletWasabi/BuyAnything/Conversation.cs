using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using WalletWasabi.WebClients.BuyAnything;
using WalletWasabi.WebClients.ShopWare.Models;
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
			var source = isMine ? MessageSource.User : MessageSource.Bot;
			string text = EnsureProperRawMessage(items[1]);
			bool isUnread = !oldConversation.Contains(text, isMine);
			var data = oldConversation.FirstOrDefault(x => x.Text == text)?.Data;
			chatEntries.Add(new ChatMessage(source, text, isUnread, null, data));
		}

		//var systemMessages = oldConversation.Where(line => line is SystemChatMessage);
		//foreach (var message in systemMessages)
		//{
		//	int index = oldConversation.ToList().IndexOf(message);
		//	chatEntries[index] = message;
		//}

		return new Chat(chatEntries);
	}

	public string ToText()
	{
		StringBuilder result = new();

		foreach (var chatMessage in this)
		{
			var prefix = chatMessage.IsMyMessage ? "WASABI" : "SIB";
			result.Append($"||#{prefix}#{EnsureProperRawMessage(chatMessage.Text)}");
		}

		result.Append("||");

		return result.ToString();
	}

	public bool Contains(string singleMessage, bool isMine)
	{
		foreach (var chatMessage in this)
		{
			if (chatMessage.IsMyMessage == isMine && chatMessage.Text == singleMessage)
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

public record ConversationMetaData(
	string Title,
	BuyAnythingClient.Product? Product = null,
	Country? Country = null,
	string? RequestedItem = null,
	bool PrivacyPolicyAccepted = false,
	bool OfferReceived = false,
	string? FirstName = null,
	string? LastName = null,
	string? StreetName = null,
	string? HouseNumber = null,
	string? PostalCode = null,
	string? City = null,
	State? State = null,
	bool TermsAccepted = false,
	bool OfferAccepted = false,
	bool PaymentConfirmed = false);
