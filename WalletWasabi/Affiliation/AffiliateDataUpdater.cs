using System.Linq;
using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Affiliation.Models;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using WalletWasabi.Affiliation.Models.CoinJoinNotification;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Affiliation;

public class AffiliateDataUpdater : BackgroundService
{
	private static readonly TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(60);
	public AffiliateDataUpdater(IRoundNotifier roundNotificationSource, ImmutableDictionary<string, AffiliateServerHttpApiClient> clients, AffiliationMessageSigner signer)
	{
		RoundNotificationSource = roundNotificationSource;
		Clients = clients;
		Signer = signer;
	}

	private IRoundNotifier RoundNotificationSource { get; }
	private AffiliationMessageSigner Signer { get; }
	private ImmutableDictionary<string, AffiliateServerHttpApiClient> Clients { get; }
	private ConcurrentDictionary<uint256, ConcurrentDictionary<string, byte[]>> AffiliateData { get; } = new();

	public ImmutableDictionary<string, ImmutableDictionary<string, byte[]>> GetAffiliateData()
	{
		return AffiliateData.ToImmutableDictionary(
			x => x.Key.ToString(),
			x => x.Value.ToImmutableDictionary());
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			IAsyncEnumerable<RoundNotification> roundNotifications = RoundNotificationSource.GetRoundNotifications(cancellationToken);
			await foreach (RoundNotification roundNotification in roundNotifications.ConfigureAwait(false))
			{
				try
				{
					switch (roundNotification)
					{
						case RoundBuiltTransactionNotification notification:
							AddCoinJoinRequestFor(notification.RoundId);
							await UpdateAffiliateDataAsync(notification.RoundId, notification.BuiltTransactionData, cancellationToken).ConfigureAwait(false);
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
		if (!AffiliateData.TryAdd(roundId, new ConcurrentDictionary<string, byte[]>()))
		{
			throw new InvalidOperationException();
		}
	}

	private void RemoveCoinJoinRequestForRound(uint256 roundId)
	{
		if (!AffiliateData.Remove(roundId, out _))
		{
			// This can occur if the round is finished before coinjoin requests are updated.
			Logger.LogInfo($"The round ({roundId}) does not exist.");
		}
	}

	private async Task UpdateAffiliateDataAsync(uint256 roundId, BuiltTransactionData builtTransactionData, CancellationToken cancellationToken)
	{
		var updateTasks = Clients.Select(
			x => UpdateAffiliateDataAsync(roundId, builtTransactionData, x.Key, x.Value, cancellationToken));
		await Task.WhenAll(updateTasks).ConfigureAwait(false);
	}

	private async Task UpdateAffiliateDataAsync(uint256 roundId, BuiltTransactionData builtTransactionData, string affiliationId, AffiliateServerHttpApiClient affiliateServerHttpApiClient, CancellationToken cancellationToken)
	{
		try
		{
			Body body = builtTransactionData.GetAffiliationData(affiliationId);
			byte[] result = await GetCoinJoinRequestAsync(affiliateServerHttpApiClient, body, cancellationToken).ConfigureAwait(false);

			RegisterReceivedCoinJoinRequest(roundId, affiliationId, result);
		}
		catch (Exception exception)
		{
			Logger.LogError($"Cannot update coinjoin request for round ({roundId}) and affiliate flag '{affiliationId}': {exception}");
		}
	}

	private void RegisterReceivedCoinJoinRequest(uint256 roundId, string affiliationId, byte[] coinjoinRequestResponse)
	{
		if (AffiliateData.TryGetValue(roundId, out ConcurrentDictionary<string, byte[]>? coinjoinRequests))
		{
			if (!coinjoinRequests.TryAdd(affiliationId, coinjoinRequestResponse))
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
		CoinJoinNotificationRequest coinJoinRequestRequest = new(body, signature);
		
		using CancellationTokenSource linkedCts = cancellationToken.CreateLinkedTokenSourceWithTimeout(AffiliateServerTimeout);
		CoinJoinNotificationResponse coinJoinNotificationResponse = await client.NotifyCoinJoinAsync(coinJoinRequestRequest, linkedCts.Token).ConfigureAwait(false);
		return coinJoinNotificationResponse.AffiliateData;
	}

	public override void Dispose()
	{
		RoundNotificationSource.Dispose();
		Signer.Dispose();
		base.Dispose();
	}
}
