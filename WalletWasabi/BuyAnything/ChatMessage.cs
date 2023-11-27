using System.Collections.Generic;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.BuyAnything;

public abstract record DataCarrier
{
	public static readonly DataCarrier NoData = new NoData();
}

public record Invoice(string Bip21Link, decimal Amount, string BitcoinAddress, bool IsPaid) : DataCarrier;

public record OfferItem(float Quantity, string Description, float UnitPrice, float TotalPrice);

public record OfferCarrier(IEnumerable<OfferItem> Items, ShippingCosts ShippingCost) : DataCarrier;

public record TrackingCodes(IEnumerable<string> Codes) : DataCarrier;

public record AttachmentLinks(IEnumerable<string> Links) : DataCarrier;

public record NoData : DataCarrier;

public record ChatMessage(MessageSource Source, string Text, bool IsUnread, string? StepName, DataCarrier? Data = null)
{
	public bool IsMyMessage => Source == MessageSource.User;
}

public enum MessageSource
{
	User = 1,
	Agent = 2,
	Bot = 3
}
