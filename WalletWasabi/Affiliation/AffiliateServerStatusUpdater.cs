using System.Linq;
using System.Collections.Immutable;
using System.Collections.Generic;
using WalletWasabi.Affiliation.Models;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;

namespace WalletWasabi.Affiliation;

public class AffiliateServerStatusUpdater : PeriodicRunner
{
	private static TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(20);
	private static TimeSpan Interval = TimeSpan.FromMinutes(1);

	public AffiliateServerStatusUpdater(IDictionary<AffiliationFlag, AffiliateServerHttpApiClient> clients)
		  : base(Interval)
	{
		Clients = clients;
		RunningAffiliateServers = ImmutableList<AffiliationFlag>.Empty;
	}

	private IDictionary<AffiliationFlag, AffiliateServerHttpApiClient> Clients { get; }
	public ImmutableList<AffiliationFlag> RunningAffiliateServers { get; private set; }

	public IEnumerable<AffiliationFlag> GetRunningAffiliateServers()
	{
		return RunningAffiliateServers;
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		await UpdateRunningAffiliateServersAsync();
	}

	private static async Task<bool> IsAffiliateServerRunning(AffiliateServerHttpApiClient client, CancellationToken cancellationToken)
	{
		try
		{
			StatusResponse result = await client.GetStatus(new StatusRequest(), cancellationToken);
			return true;
		}
		catch (Exception exception)
		{
			Logging.Logger.LogError(exception.Message);
			return false;
		}
	}

	private async Task<bool> IsAffiliateServerRunning(AffiliateServerHttpApiClient client)
	{
		using CancellationTokenSource cancellationTokenSource = new(AffiliateServerTimeout);
		CancellationToken cancellationToken = cancellationTokenSource.Token;
		return await IsAffiliateServerRunning(client, cancellationToken);
	}

	private async Task UpdateRunningAffiliateServersAsync(AffiliationFlag affiliationFlag, AffiliateServerHttpApiClient affiliateServerHttpApiClient)
	{
		try
		{
			if (await IsAffiliateServerRunning(affiliateServerHttpApiClient))
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
		catch (Exception exception)
		{
			Logging.Logger.LogError(exception.Message);
		}
	}

	private async Task UpdateRunningAffiliateServersAsync()
	{
		await Task.WhenAll(Clients.Select(x => UpdateRunningAffiliateServersAsync(x.Key, x.Value)));
	}
}
