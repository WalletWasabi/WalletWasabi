using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public class OfferMessageViewModel : AssistantMessageViewModel
{
	public OfferMessageViewModel(OfferCarrier offerCarrier, ChatMessageMetaData metaData) : base(null, null, metaData)
	{
		OfferCarrier = offerCarrier;
	}

	public OfferCarrier OfferCarrier { get; }
}
