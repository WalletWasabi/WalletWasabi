using System.Reactive.Linq;

namespace WalletWasabi.Fluent.AppServices.Tor;

public class TorNetwork : ITorNetwork
{
	private static readonly Uri RssFeedUri = new("https://status.torproject.org/index.xml");

	public TorNetwork(IHttpGetStringReader httpGetReader, IIssueListParser issueListParser)
	{
		var observable = Observable
			.FromAsync(() => httpGetReader.Read(RssFeedUri))
			.SelectMany(content => issueListParser.Parse(content).ToObservable());
		Issues = observable;
	}

	public IObservable<Issue> Issues { get; }
}
