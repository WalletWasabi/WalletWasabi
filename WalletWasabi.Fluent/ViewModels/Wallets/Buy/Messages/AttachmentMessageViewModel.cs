using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

internal class AttachmentMessageViewModel : MessageViewModel
{
	public AttachmentMessageViewModel(AttachmentLinks attachmentLinks, ChatMessageMetaData metaData) : base(null, null, metaData)
	{
		Links = attachmentLinks.Codes.Select(url => new HyperlinkViewModel(url, url));
	}

	public IEnumerable<HyperlinkViewModel> Links { get; }
}

internal class HyperlinkViewModel
{
	public string Text { get; }
	public string Url { get; }

	public HyperlinkViewModel(string text, string url)
	{
		Text = text;
		Url = url;
		OpenLinkCommand = ReactiveCommand.Create(() => IoHelpers.OpenBrowserAsync(url));
	}

	public ICommand OpenLinkCommand { get; set; }
}
