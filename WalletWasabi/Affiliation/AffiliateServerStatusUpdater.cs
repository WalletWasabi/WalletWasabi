using System.Linq;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Net.Http;
using WalletWasabi.Affiliation.Models;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;

namespace WalletWasabi.Affiliation;

public class AffiliateServerStatusUpdater : PeriodicRunner
{
	private static readonly TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

	public AffiliateServerStatusUpdater(IDictionary<AffiliationFlag, AffiliateServerHttpApiClient> clients)
		  : base(Interval)
	{
		Clients = clients;
		RunningAffiliateServers = ImmutableList<AffiliationFlag>.Empty;
	}

	private IDictionary<AffiliationFlag, AffiliateServerHttpApiClient> Clients { get; }
	private ImmutableList<AffiliationFlag> RunningAffiliateServers { get; set; }

	public ImmutableArray<AffiliationFlag> GetRunningAffiliateServers()
	{
		return RunningAffiliateServers.ToImmutableArray();
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		await UpdateRunningAffiliateServersAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task<bool> IsAffiliateServerRunningAsync(AffiliateServerHttpApiClient client, CancellationToken cancellationToken)
	{
		bool IsCatchableException(Exception exception)
		{
			return exception is HttpRequestException || exception is OperationCanceledException;
		}

		IEnumerable<Exception> UnfoldException(Exception exception)
		{
			if (exception is AggregateException)
			{
				return ((AggregateException)exception).InnerExceptions;
			}

			return new[] { exception };
		}

		try
		{
			StatusResponse result = await client.GetStatusAsync(new StatusRequest(), cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (Exception exception)
		{
			if (UnfoldException(exception).All(IsCatchableException))
			{
				Logging.Logger.LogError(exception);
				return false;
			}
			else
			{
				throw exception;
			}
		}
	}

	private async Task UpdateRunningAffiliateServersAsync(AffiliationFlag affiliationFlag, AffiliateServerHttpApiClient affiliateServerHttpApiClient, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCTS = new(AffiliateServerTimeout);
		using CancellationTokenSource linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCTS.Token);
		if (await IsAffiliateServerRunningAsync(affiliateServerHttpApiClient, linkedCTS.Token).ConfigureAwait(false))
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
