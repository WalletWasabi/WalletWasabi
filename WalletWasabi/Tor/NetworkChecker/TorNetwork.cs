using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.Tor.NetworkChecker;

public class TorNetwork : ITorNetwork
{
	private static readonly Uri IssuesPath = new("https://gitlab.torproject.org/api/v4/projects/786/repository/tree?path=content/issues");
	private static readonly Uri IssuesRoot = new("https://gitlab.torproject.org/tpo/tpa/status-site/-/raw/main/");

	private readonly IIssueParser _issueParser;
	private readonly IUriBasedStringStore _stringStore;

	public TorNetwork(IUriBasedStringStore stringStore, IIssueParser issueParser)
	{
		_stringStore = stringStore;
		_issueParser = issueParser;

		Issues = GetIssueFilenames()
			.SelectMany(
				uris => uris.ToObservable()
					.SelectMany(GetIssueFromUri));
	}

	public IObservable<Issue> Issues { get; }

	private static IObservable<Uri> ParseResponse(string responseText)
	{
		return JArray.Parse(responseText)
			.Select(d => d["path"])
			.Select(x => x!.ToString())
			.Where(x => x.EndsWith(".md"))
			.Select(filename => new Uri(IssuesRoot, filename))
			.ToObservable();
	}

	private IObservable<IList<Uri>> GetIssueFilenames()
	{
		var input = Observable
			.FromAsync(() => _stringStore.Fetch(IssuesPath))
			.SelectMany(responseText => ParseResponse(responseText).ToList());

		return input;
	}

	private IObservable<Issue> GetIssueFromUri(Uri path)
	{
		var observable = Observable
			.FromAsync(() => _stringStore.Fetch(path))
			.Select(GetIssueFromContent);
		return observable;
	}

	private Issue GetIssueFromContent(string content)
	{
		return _issueParser.Parse(content);
	}
}
