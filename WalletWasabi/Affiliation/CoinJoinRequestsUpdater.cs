using System.Linq;
using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Affiliation.Models;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Events;
using System.Collections.Concurrent;
using WalletWasabi.Affiliation.Extensions;
using WalletWasabi.Affiliation.Models.CoinjoinRequest;
using WalletWasabi.Logging;

namespace WalletWasabi.Affiliation;

public class CoinJoinRequestsUpdater : BackgroundService
{
	private static readonly TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(60);
	public CoinJoinRequestsUpdater(Arena arena, ImmutableDictionary<string, AffiliateServerHttpApiClient> clients, AffiliationMessageSigner signer)
	{
		Arena = arena;
		Clients = clients;
		Signer = signer;

		AddHandlers();
	}

	private Arena Arena { get; }
	private AffiliationMessageSigner Signer { get; }
	private ImmutableDictionary<string, AffiliateServerHttpApiClient> Clients { get; }
	private ConcurrentDictionary<uint256, ConcurrentDictionary<string, byte[]>> CoinJoinRequests { get; } = new();
	private ConcurrentDictionary<uint256, RoundData> RoundData { get; } = new();
	private AsyncQueue<FinalizedRoundDataWithRoundId> RoundsToUpdate { get; } = new();

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
			await foreach (FinalizedRoundDataWithRoundId finalizedRoundDataWithRoundId in RoundsToUpdate.GetAsyncIterator(cancellationToken).ConfigureAwait(false))
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

	private void RemoveRound(uint256 roundId)
	{
		if (!RoundData.TryRemove(roundId, out _))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		if (!CoinJoinRequests.TryRemove(roundId, out _))
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
		return await GetCoinJoinRequestAsync(client, coinjoinRequestRequest, cancellationToken).ConfigureAwait(false);
	}

	private async Task<byte[]> GetCoinJoinRequestAsync(AffiliateServerHttpApiClient client, GetCoinjoinRequestRequest getCoinjoinRequestRequest, CancellationToken cancellationToken)
	{
		using CancellationTokenSource linkedCts = cancellationToken.CreateLinkedTokenSourceWithTimeout(AffiliateServerTimeout);
		
		GetCoinJoinRequestResponse getCoinJoinRequestResponse = await client.GetCoinJoinRequestAsync(getCoinjoinRequestRequest, linkedCts.Token).ConfigureAwait(false);
		return getCoinJoinRequestResponse.CoinjoinRequest;
	}

	private void CreateRound(uint256 roundId, RoundParameters roundParameters)
	{
		RoundData roundData = new(roundParameters);

		if (!RoundData.TryAdd(roundId, roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) already exist.");
		}
	}

	private void Arena_InputAdded(object? sender, InputAddedEventArgs args)
	{
		GetRoundDataOrFail(args.RoundId)
			.AddInputCoin(args.Coin, args.IsCoordinationFeeExempted);
	}

	private void Arena_AffiliationAdded(object? sender, AffiliationAddedEventArgs args)
	{
		GetRoundDataOrFail(args.RoundId)
			.AddInputAffiliationFlag(args.Coin, args.AffiliationFlag);
	}

	private void Arena_RoundCreated(object? sender, RoundCreatedEventArgs args)
	{
		CreateRound(args.RoundId, args.RoundParameters);
	}

	private void ArenaCoinJoinTransactionAdded(object? sender, CoinJoinTransactionCreatedEventArgs args)
	{
		RoundData roundData = GetRoundDataOrFail(args.RoundId);
		RoundsToUpdate.Enqueue(new FinalizedRoundDataWithRoundId(args.RoundId, roundData.FinalizeRoundData(args.Transaction)));
	}

	private void Arena_RoundPhaseChanged(object? sender, RoundPhaseChangedEventArgs args)
	{
		if (args.Phase == Phase.Ended)
		{
			RemoveRound(args.RoundId);
		}
	}

	private RoundData GetRoundDataOrFail(uint256 roundId)
	{
		if (!RoundData.TryGetValue(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		return roundData;
	}
	
	private void AddHandlers()
	{
		Arena.RoundCreated += Arena_RoundCreated;
		Arena.AffiliationAdded += Arena_AffiliationAdded;
		Arena.InputAdded += Arena_InputAdded;
		Arena.CoinJoinTransactionCreated += ArenaCoinJoinTransactionAdded;
		Arena.RoundPhaseChanged += Arena_RoundPhaseChanged;
	}

	private void RemoveHandlers()
	{
		Arena.RoundCreated -= Arena_RoundCreated;
		Arena.AffiliationAdded -= Arena_AffiliationAdded;
		Arena.InputAdded -= Arena_InputAdded;
		Arena.CoinJoinTransactionCreated -= ArenaCoinJoinTransactionAdded;
		Arena.RoundPhaseChanged -= Arena_RoundPhaseChanged;
	}

	public override void Dispose()
	{
		RemoveHandlers();
		Signer.Dispose();
		base.Dispose();
	}

	private record FinalizedRoundDataWithRoundId(uint256 RoundId, FinalizedRoundData FinalizedRoundData);
}
