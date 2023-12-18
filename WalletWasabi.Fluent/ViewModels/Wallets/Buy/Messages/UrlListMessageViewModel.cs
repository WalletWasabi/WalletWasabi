using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;
using WalletWasabi.BuyAnything;
using WalletWasabi.Helpers;

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

internal class HyperlinkViewModel
{
	public string Text { get; }
	public string Url { get; }

	public HyperlinkViewModel(string text, string url)
	{
		Text = text;
		Url = url;
		OpenLinkCommand = ReactiveCommand.Create(() => IoHelpers.OpenBrowserAsync(url));
		CopyCommand = ReactiveCommand.CreateFromTask(() => Application.Current?.Clipboard?.SetTextAsync(Url) ?? Task.CompletedTask);
	}

	public ICommand OpenLinkCommand { get; set; }

	public ICommand CopyCommand { get; set; }
}
