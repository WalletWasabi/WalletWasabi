using System.Linq;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public class OfferMessageViewModel : AssistantMessageViewModel
{
	public OfferMessageViewModel(OfferCarrier offerCarrier, ChatMessageMetaData metaData) : base(null, null, metaData)
	{
		OfferCarrier = offerCarrier;

		var shippingCost = float.Parse(OfferCarrier.ShippingCost.TotalPrice);
		var total = OfferCarrier.Items.Sum(x => x.TotalPrice) + shippingCost;
		TotalMessage = $"For a total price of {total} USD, which includes {shippingCost} USD shipping cost.";
	}

	public OfferCarrier OfferCarrier { get; }

	public string TotalMessage { get; }
}
