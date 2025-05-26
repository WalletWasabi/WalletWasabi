using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
using WalletWasabi.Services;

namespace WalletWasabi.Tor.StatusChecker;

/// <summary>
/// Component that periodically checks https://status.torproject.org/ to detect network disruptions.
/// </summary>
public class TorStatusChecker : PeriodicRunner
{
	private readonly EventBus _eventBus;
	private static readonly Uri TorStatusUri = new("https://status.torproject.org/index.json");

	public TorStatusChecker(TimeSpan period, HttpClient httpClient, EventBus eventBus)
		: base(period)
	{
		_eventBus = eventBus;
		_httpClient = httpClient;
	}

	private readonly HttpClient _httpClient;

	/// <inheritdoc/>
	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, TorStatusUri);
			using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			using var content = response.Content;

			TorNetworkStatus systemsStatus = await content.ReadAsJsonAsync(Decode.TorStatus).ConfigureAwait(false);
			Issue[] issues = CheckForTorIssues(systemsStatus);

			// Fire event.
			_eventBus.Publish(new TorNetworkStatusChanged(issues));
		}
		catch (Exception ex)
		{
			Logger.LogDebug("Failed to get/parse Tor status page.", ex);
		}
	}

	/// <summary>
	/// Checks the relevant systems for any issues.
	/// Relevant systems are: v3 Onion Services, Directory Authorities and DNS.
	/// </summary>
	private Issue[] CheckForTorIssues(TorNetworkStatus systemsStatus)
	{
		string[] systemNames = { "v3 Onion Service", "Directory Authorities", "DNS" };

		var issues = systemsStatus.Systems
			.Where(system => systemNames.Contains(system.Name))
			.Where(system => system.Status != "ok" || system.UnresolvedIssues.Count != 0)
			.SelectMany(system =>
				system.UnresolvedIssues
					.Where(issue => !issue.Resolved)
					.Select(issue => new Issue(system.Name, issue.Title, resolved: false)))
			.ToArray();

		return issues;
	}
}
