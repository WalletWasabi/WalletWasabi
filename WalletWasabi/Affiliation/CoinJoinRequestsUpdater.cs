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

public record FinalizedRoundDataWithRoundId(uint256 RoundId, FinalizedRoundData FinalizedRoundData);

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
		return CoinJoinRequests.ToDictionary(
			x => x.Key.ToString(),
			x => x.Value.ToImmutableDictionary())
			.ToImmutableDictionary();
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			IAsyncEnumerable<FinalizedRoundDataWithRoundId> finalizedRounds = AffiliateDataCollector.GetFinalizedRounds(cancellationToken);
			await foreach (FinalizedRoundDataWithRoundId finalizedRoundDataWithRoundId in finalizedRounds.ConfigureAwait(false))
			{
				try
				{
					await UpdateCoinJoinRequestsAsync(finalizedRoundDataWithRoundId.RoundId, finalizedRoundDataWithRoundId.FinalizedRoundData, cancellationToken).ConfigureAwait(false);
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

	private async Task UpdateCoinJoinRequestsAsync(uint256 roundId, FinalizedRoundData finalizedRoundData, CancellationToken cancellationToken)
	{
		if (!CoinJoinRequests.TryAdd(roundId, new ConcurrentDictionary<string, byte[]>()))
		{
			throw new InvalidOperationException();
		}
		await Task.WhenAll(Clients.Select(x => UpdateCoinJoinRequestsAsync(roundId, finalizedRoundData, x.Key, x.Value, cancellationToken))).ConfigureAwait(false);
	}

	private async Task UpdateCoinJoinRequestsAsync(uint256 roundId, FinalizedRoundData finalizedRoundData, string affiliationFlag, AffiliateServerHttpApiClient affiliateServerHttpApiClient, CancellationToken cancellationToken)
	{
		try
		{
			Body body = finalizedRoundData.GetAffiliationData(affiliationFlag);
			byte[] result = await GetCoinJoinRequestAsync(affiliateServerHttpApiClient, body, cancellationToken).ConfigureAwait(false);

			if (CoinJoinRequests.TryGetValue(roundId, out ConcurrentDictionary<string, byte[]>? coinjoinRequests))
			{
				if (!coinjoinRequests.TryAdd(affiliationFlag, result))
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
		catch (Exception exception)
		{
			Logger.LogError($"Cannot update coinjoin request for round ({roundId}) and affiliate flag '{affiliationFlag}': {exception}");
		}
	}

	private async Task<byte[]> GetCoinJoinRequestAsync(AffiliateServerHttpApiClient client, Body body, CancellationToken cancellationToken)
	{
		Payload payload = new(Header.Instance, body);
		byte[] signature = Signer.Sign(payload.GetCanonicalSerialization());
		GetCoinjoinRequestRequest coinjoinRequestRequest = new(body, signature);
		return await GetCoinJoinRequestAsync(client, coinjoinRequestRequest, cancellationToken).ConfigureAwait(false);
	}

	private async Task<byte[]> GetCoinJoinRequestAsync(AffiliateServerHttpApiClient client, GetCoinjoinRequestRequest getCoinjoinRequestRequest, CancellationToken cancellationToken)
	{
		using CancellationTokenSource linkedCts = cancellationToken.CreateLinkedTokenSourceWithTimeout(AffiliateServerTimeout);
		
		GetCoinJoinRequestResponse getCoinJoinRequestResponse = await client.GetCoinJoinRequestAsync(getCoinjoinRequestRequest, linkedCts.Token).ConfigureAwait(false);
		return getCoinJoinRequestResponse.CoinjoinRequest;
	}

	public override void Dispose()
	{
		AffiliateDataCollector.Dispose();
		Signer.Dispose();
		base.Dispose();
	}
}
