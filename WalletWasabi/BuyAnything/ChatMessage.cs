using System.Collections.Generic;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.BuyAnything;

public abstract record DataCarrier
{
	public static readonly DataCarrier NoData = new NoData();
}

public record Invoice(string Bip21Link, decimal Amount, string BitcoinAddress) : DataCarrier;

public record OfferItem(float Quantity, string Description, float UnitPrice, float TotalPrice);

public record OfferCarrier(IEnumerable<OfferItem> Items) : DataCarrier;

public record TrackingCodes(IEnumerable<string> Codes) : DataCarrier;

public record AttachmentLinks(IEnumerable<string> Links) : DataCarrier;

public record NoData : DataCarrier;

public record ChatMessageMetaData(ChatMessageMetaData.ChatMessageTag Tag, bool IsPaid = false)
{
	public static readonly ChatMessageMetaData Empty = new(ChatMessageTag.None);

	public enum ChatMessageTag
	{
		None = 0,

		AssistantType = 11,
		Country = 12,

		FirstName = 21,
		LastName = 22,
		StreetName = 23,
		HouseNumber = 24,
		PostalCode = 25,
		City = 26,
		State = 27,
	}
}

public record ChatMessage(bool IsMyMessage, string Message, bool IsUnread, ChatMessageMetaData MetaData);

public record ChatMessage2(MessageSource Source, string Message, bool IsUnread, string? StepName, DataCarrier? Data = null)
{
	public bool IsMyMessage => Source == MessageSource.User;
}

public enum MessageSource
{
	User = 1,
	Agent = 2,
	Bot = 3
}

public record SystemChatMessage(string Message, DataCarrier Data, bool IsUnread, ChatMessageMetaData MetaData)
	: ChatMessage(false, Message, IsUnread, MetaData);
