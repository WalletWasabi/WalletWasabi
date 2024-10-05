using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tor.StatusChecker;

/// <summary>
/// Component that periodically checks https://status.torproject.org/ to detect network disruptions.
/// </summary>
public class TorStatusChecker : PeriodicRunner
{
	private readonly XmlIssueListParser _parser;
	private static readonly Uri TorStatusUri = new("https://status.torproject.org/index.xml");

	public TorStatusChecker(TimeSpan period, HttpClient httpClient, XmlIssueListParser parser)
		: base(period)
	{
		_parser = parser;
		_httpClient = httpClient;
	}

	public event EventHandler<Issue[]>? StatusEvent;

	private readonly HttpClient _httpClient;

	/// <inheritdoc/>
	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, TorStatusUri);
			using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

			string xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var issues = _parser.Parse(xml);

			// Fire event.
			StatusEvent?.Invoke(this, issues.ToArray());
		}
		catch (Exception ex)
		{
			Logger.LogDebug("Failed to get/parse Tor status page.", ex);
		}
	}
}
