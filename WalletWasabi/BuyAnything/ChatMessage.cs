using System.Collections.Generic;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.BuyAnything;

public abstract record DataCarrier
{
	public static DataCarrier NoData = new NoData();
}

public record Invoice(string Bip21Link, decimal Amount, string BitcoinAddress) : DataCarrier;

public record OfferItem(float Quantity, string Description, float UnitPrice, float TotalPrice);

public record OfferCarrier(IEnumerable<OfferItem> Items, ShippingCosts ShippingCost) : DataCarrier;

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

public record SystemChatMessage(string Message, DataCarrier Data, bool IsUnread, ChatMessageMetaData MetaData)
	: ChatMessage(false, Message, IsUnread, MetaData);
