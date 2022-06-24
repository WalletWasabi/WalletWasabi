using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels.StatusIcon;

namespace WalletWasabi.Fluent.AppServices.Tor;

public class TorNetwork
{
	private static readonly Uri RssFeedUri = new("https://status.torproject.org/index.xml");

	public TorNetwork(HttpGetStringReader httpGetReader, XmlIssueListParser issueListParser)
	{
		var observable = Observable
			.FromAsync(() => httpGetReader.ReadAsync(RssFeedUri))
			.SelectMany(content => issueListParser.Parse(content).ToObservable());
		Issues = observable;
	}

	public IObservable<Issue> Issues { get; }
}
