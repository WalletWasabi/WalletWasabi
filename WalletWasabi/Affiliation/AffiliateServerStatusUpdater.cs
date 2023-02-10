using System.Linq;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Affiliation.Extensions;
using WalletWasabi.Bases;

namespace WalletWasabi.Affiliation;

public class AffiliateServerStatusUpdater : PeriodicRunner
{
	private static readonly TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

	public AffiliateServerStatusUpdater(IDictionary<string, AffiliateServerHttpApiClient> clients)
		  : base(Interval)
	{
		Clients = clients;
		RunningAffiliateServers = ImmutableList<string>.Empty;
	}

	private IDictionary<string, AffiliateServerHttpApiClient> Clients { get; }
	private ImmutableList<string> RunningAffiliateServers { get; set; }

	public ImmutableArray<string> GetRunningAffiliateServers()
	{
		return RunningAffiliateServers.ToImmutableArray();
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		await UpdateRunningAffiliateServersAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task<bool> IsAffiliateServerRunningAsync(AffiliateServerHttpApiClient client, CancellationToken cancellationToken)
	{
		try
		{
			await client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (Exception exception)
		{
			Logging.Logger.LogError(exception);
			return false;
		}
	}

	private async Task UpdateRunningAffiliateServersAsync(string affiliationFlag, AffiliateServerHttpApiClient affiliateServerHttpApiClient, CancellationToken cancellationToken)
	{
		using var linkedCts = cancellationToken.CreateLinkedTokenSourceWithTimeout(AffiliateServerTimeout);
		
		if (await IsAffiliateServerRunningAsync(affiliateServerHttpApiClient, linkedCts.Token).ConfigureAwait(false))
		{
			if (!RunningAffiliateServers.Contains(affiliationFlag))
			{
				RunningAffiliateServers = RunningAffiliateServers.Add(affiliationFlag);
				Logging.Logger.LogInfo($"Affiliate server '{affiliationFlag}' went up.");
			}
			else
			{
				Logging.Logger.LogDebug($"Affiliate server '{affiliationFlag}' is running.");
			}
		}
		else
		{
			if (RunningAffiliateServers.Contains(affiliationFlag))
			{
				RunningAffiliateServers = RunningAffiliateServers.Remove(affiliationFlag);
				Logging.Logger.LogWarning($"Affiliate server '{affiliationFlag}' went down.");
			}
			else
			{
				Logging.Logger.LogDebug($"Affiliate server '{affiliationFlag}' is not running.");
			}
		}
	}

	private async Task UpdateRunningAffiliateServersAsync(CancellationToken cancellationToken)
	{
		await Task.WhenAll(Clients.Select(x => UpdateRunningAffiliateServersAsync(x.Key, x.Value, cancellationToken))).ConfigureAwait(false);
	}
}
