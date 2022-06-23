using System.Reactive.Linq;

namespace WalletWasabi.Tor.NetworkChecker;

public class TorNetwork : ITorNetwork
{
	private static readonly Uri RssFeedUri = new("https://status.torproject.org/index.xml");

	public TorNetwork(IHttpGetTextReader stringStore, IIssueListParser issueListParser)
	{
		var observable = Observable
			.FromAsync(() => stringStore.Read(RssFeedUri))
			.SelectMany(content => issueListParser.Parse(content).ToObservable());
		Issues = observable;
	}
	
	public IObservable<Issue> Issues { get; }
}
