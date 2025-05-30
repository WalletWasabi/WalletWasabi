using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
using WalletWasabi.Services;

namespace WalletWasabi.Tor.StatusChecker;

public static class TorStatusChecker
{
	private static readonly string[] RelevantSystems = { "v3 Onion Services", "Directory Authorities", "DNS" };
	private static readonly Uri TorStatusUri = new("https://status.torproject.org/index.json");

	public record CheckMessage;

	public static Func<CheckMessage, CancellationToken, Task<Unit>> CreateChecker(HttpClient httpClient, EventBus eventBus) =>
		(_, cancellationToken) => CheckTorStatusAsync(httpClient, eventBus, cancellationToken);

	private static async Task<Unit> CheckTorStatusAsync(HttpClient httpClient, EventBus eventBus, CancellationToken cancellationToken)
	{
		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, TorStatusUri);
			using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			using var content = response.Content;

			var systemsStatus = await content.ReadAsJsonAsync(Decode.TorStatus).ConfigureAwait(false);
			var issues = CheckForTorIssues(systemsStatus);

			// Fire event.
			eventBus.Publish(new TorNetworkStatusChanged(issues));
		}
		catch (Exception ex)
		{
			Logger.LogDebug("Failed to get/parse Tor status page.", ex);
		}

		return Unit.Instance;
	}

	/// <summary>
	/// Checks the relevant systems for any issues.
	/// Relevant systems are: v3 Onion Services, Directory Authorities and DNS.
	/// </summary>
	private static Issue[] CheckForTorIssues(TorNetworkStatus systemsStatus)
	{
		var issues = systemsStatus.Systems
			.Where(system => RelevantSystems.Contains(system.Name))
			.Where(system => system.Status != "ok" || system.UnresolvedIssues.Count != 0)
			.SelectMany(system =>
				system.UnresolvedIssues
					.Where(issue => !issue.Resolved)
					.Select(issue => new Issue(system.Name, issue.Title, resolved: false)))
					.ToArray();

		return issues;
	}
}
