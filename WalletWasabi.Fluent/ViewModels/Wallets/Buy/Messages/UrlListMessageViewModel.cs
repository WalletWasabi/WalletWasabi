using System.Collections.Generic;
using System.Linq;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

internal class UrlListMessageViewModel : AssistantMessageViewModel
{
	public UrlListMessageViewModel(ChatMessage message, string uiMessage) : base(message)
	{
		Data = message.Data;
		UiMessage = uiMessage;
		Links = Data switch
		{
			AttachmentLinks linkData => linkData.Links.Select(url => new HyperlinkViewModel(url, url)),
			TrackingCodes trackingData => trackingData.Codes.Select(url => new HyperlinkViewModel(url, url)),
			_ => Enumerable.Empty<HyperlinkViewModel>()
		};
	}

	public DataCarrier? Data { get; }

	public IEnumerable<HyperlinkViewModel> Links { get; }
}
