using System.Linq;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public class OfferMessageViewModel : AssistantMessageViewModel
{
	public OfferMessageViewModel(ChatMessage message) : base(message)
	{
		if (message.Data is not OfferCarrier carrier)
		{
			throw new InvalidOperationException($"Invalid Data Type: {message.Data?.GetType().Name}");
		}

		OfferCarrier = carrier;

		var shippingCost = float.Parse(OfferCarrier.ShippingCost.TotalPrice);
		var total = OfferCarrier.Items.Sum(x => x.TotalPrice) + shippingCost;
		TotalMessage = $"For a total price of {total} USD, which includes {shippingCost} USD shipping cost.";

		UiMessage = "I can offer you:";
	}

	public OfferCarrier OfferCarrier { get; }

	public string TotalMessage { get; }
}
