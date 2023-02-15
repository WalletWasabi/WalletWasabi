using System.Linq;
using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Affiliation.Models;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using WalletWasabi.Affiliation.Extensions;
using WalletWasabi.Affiliation.Models.CoinjoinRequest;
using WalletWasabi.Logging;

namespace WalletWasabi.Affiliation;

public class CoinJoinRequestsUpdater : BackgroundService
{
	private static readonly TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(60);
	public CoinJoinRequestsUpdater(Arena arena, ImmutableDictionary<string, AffiliateServerHttpApiClient> clients, AffiliationMessageSigner signer)
	{
		AffiliateDataCollector = new(arena);
		Clients = clients;
		Signer = signer;
	}

	private AffiliateDataCollector AffiliateDataCollector { get; }
	private AffiliationMessageSigner Signer { get; }
	private ImmutableDictionary<string, AffiliateServerHttpApiClient> Clients { get; }
	private ConcurrentDictionary<uint256, ConcurrentDictionary<string, byte[]>> CoinJoinRequests { get; } = new();

	public ImmutableDictionary<string, ImmutableDictionary<string, byte[]>> GetCoinjoinRequests()
	{
		return CoinJoinRequests.ToImmutableDictionary(
			x => x.Key.ToString(),
			x => x.Value.ToImmutableDictionary());
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			IAsyncEnumerable<RoundNotification> roundNotifications = AffiliateDataCollector.GetFinalizedRounds(cancellationToken);
			await foreach (RoundNotification roundNotification in roundNotifications.ConfigureAwait(false))
			{
				try
				{
					switch (roundNotification)
					{
						case RoundBuiltTransactionNotification notification:
							AddCoinJoinRequestFor(notification.RoundId);
							await UpdateCoinJoinRequestsAsync(notification.RoundId, notification.BuiltTransactionData, cancellationToken).ConfigureAwait(false);
							break;
						case RoundEndedNotification notification:
							RemoveCoinJoinRequestForRound(notification.RoundId);
							break;
					}
				}
				catch (Exception exception)
				{
					Logger.LogError(exception);
				}
			}
		}
		catch (TaskCanceledException)
		{
		}
	}

	private void AddCoinJoinRequestFor(uint256 roundId)
	{
		if (!CoinJoinRequests.TryAdd(roundId, new ConcurrentDictionary<string, byte[]>()))
		{
			throw new InvalidOperationException();
		}
	}

	private void RemoveCoinJoinRequestForRound(uint256 roundId)
	{
		if (!CoinJoinRequests.Remove(roundId, out _))
		{
			// This can occur if the round is finished before coinjoin requests are updated.
			Logger.LogInfo($"The round ({roundId}) does not exist.");
		}
	}

	private async Task UpdateCoinJoinRequestsAsync(uint256 roundId, BuiltTransactionData builtTransactionData, CancellationToken cancellationToken)
	{
		var updateTasks = Clients.Select(
			x => UpdateCoinJoinRequestsAsync(roundId, builtTransactionData, x.Key, x.Value, cancellationToken));
		await Task.WhenAll(updateTasks).ConfigureAwait(false);
	}

	private async Task UpdateCoinJoinRequestsAsync(uint256 roundId, BuiltTransactionData builtTransactionData, string affiliationFlag, AffiliateServerHttpApiClient affiliateServerHttpApiClient, CancellationToken cancellationToken)
	{
		try
		{
			Body body = builtTransactionData.GetAffiliationData(affiliationFlag);
			byte[] result = await GetCoinJoinRequestAsync(affiliateServerHttpApiClient, body, cancellationToken).ConfigureAwait(false);

			RegisterReceivedCoinJoinRequest(roundId, affiliationFlag, result);
		}
		catch (Exception exception)
		{
			Logger.LogError($"Cannot update coinjoin request for round ({roundId}) and affiliate flag '{affiliationFlag}': {exception}");
		}
	}

	private void RegisterReceivedCoinJoinRequest(uint256 roundId, string affiliationFlag, byte[] coinjoinRequestResponse)
	{
		if (CoinJoinRequests.TryGetValue(roundId, out ConcurrentDictionary<string, byte[]>? coinjoinRequests))
		{
			if (!coinjoinRequests.TryAdd(affiliationFlag, coinjoinRequestResponse))
			{
				throw new InvalidOperationException("The coinjoin request is already set.");
			}
		}
		else
		{
			// This can occur if the round is finished before coinjoin requests are updated.
			Logger.LogInfo($"The round ({roundId}) does not exist.");
		}
	}

	private async Task<byte[]> GetCoinJoinRequestAsync(AffiliateServerHttpApiClient client, Body body, CancellationToken cancellationToken)
	{
		Payload payload = new(Header.Instance, body);
		byte[] signature = Signer.Sign(payload.GetCanonicalSerialization());
		GetCoinjoinRequestRequest coinjoinRequestRequest = new(body, signature);
		
		using CancellationTokenSource linkedCts = cancellationToken.CreateLinkedTokenSourceWithTimeout(AffiliateServerTimeout);
		GetCoinJoinRequestResponse getCoinJoinRequestResponse = await client.GetCoinJoinRequestAsync(coinjoinRequestRequest, linkedCts.Token).ConfigureAwait(false);
		return getCoinJoinRequestResponse.CoinjoinRequest;
	}

	public override void Dispose()
	{
		AffiliateDataCollector.Dispose();
		Signer.Dispose();
		base.Dispose();
	}
}
