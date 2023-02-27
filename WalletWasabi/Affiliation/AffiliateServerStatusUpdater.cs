using System.Linq;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Affiliation.Extensions;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;

namespace WalletWasabi.Affiliation;

public class AffiliateServerStatusUpdater : PeriodicRunner
{
	private static readonly TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

	public AffiliateServerStatusUpdater(IDictionary<string, AffiliateServerHttpApiClient> clients)
		  : base(Interval)
	{
		Clients = clients;
	}

	private IDictionary<string, AffiliateServerHttpApiClient> Clients { get; }
	private HashSet<string> RunningAffiliateServers { get; } = new();
	private object RunningAffiliateServersLock { get; } = new();
	
	public ImmutableArray<string> GetRunningAffiliateServers()
	{
		lock (RunningAffiliateServersLock)
		{
			return RunningAffiliateServers.ToImmutableArray();
		}
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

	private async Task UpdateRunningAffiliateServersAsync(string affiliationId, AffiliateServerHttpApiClient affiliateServerHttpApiClient, CancellationToken cancellationToken)
	{
		using var linkedCts = cancellationToken.CreateLinkedTokenSourceWithTimeout(AffiliateServerTimeout);
		
		if (await IsAffiliateServerRunningAsync(affiliateServerHttpApiClient, linkedCts.Token).ConfigureAwait(false))
		{
			lock (RunningAffiliateServersLock)
			{
				if (RunningAffiliateServers.Add(affiliationId))
				{
					Logging.Logger.LogInfo($"Affiliate server '{affiliationId}' went up.");
				}
				else
				{
					Logging.Logger.LogDebug($"Affiliate server '{affiliationId}' is running.");
				}
			}
		}
		else
		{
			lock (RunningAffiliateServersLock)
			{
				if (RunningAffiliateServers.Remove(affiliationId))
				{
					Logging.Logger.LogWarning($"Affiliate server '{affiliationId}' went down.");
				}
				else
				{
					Logging.Logger.LogDebug($"Affiliate server '{affiliationId}' is not running.");
				}
			}
		}
	}

	private async Task UpdateRunningAffiliateServersAsync(CancellationToken cancellationToken)
	{
		var updateTasks = Clients
			.Select(x => (affiliationTag: x.Key, cffiliationClient: x.Value))
			.Select(x => UpdateRunningAffiliateServersAsync(x.affiliationTag, x.cffiliationClient, cancellationToken));
		await Task.WhenAll(updateTasks).ConfigureAwait(false);
	}
}
