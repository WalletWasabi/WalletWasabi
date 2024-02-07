using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

internal class HyperlinkViewModel
{
	public HyperlinkViewModel(string text, string url)
	{
		Text = text;
		Url = url;
		OpenLinkCommand = ReactiveCommand.Create(() => IoHelpers.OpenBrowserAsync(url));
		CopyCommand = ReactiveCommand.CreateFromTask(() => ApplicationHelper.SetTextAsync(Url) ?? Task.CompletedTask);
	}

	public string Text { get; }
	public string Url { get; }

	public ICommand OpenLinkCommand { get; set; }

	public ICommand CopyCommand { get; set; }
}
