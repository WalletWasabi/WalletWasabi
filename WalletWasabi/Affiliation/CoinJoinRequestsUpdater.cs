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
using WalletWasabi.Affiliation.Models.CoinjoinRequest;

namespace WalletWasabi.Affiliation;

public class CoinJoinRequestsUpdater : BackgroundService
{
	private static readonly TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(60);
	public CoinJoinRequestsUpdater(Arena arena, ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> clients, AffiliationMessageSigner signer)
	{
		Arena = arena;
		Clients = clients;
		Signer = signer;

		AddHandlers();
	}

	private Arena Arena { get; }
	private AffiliationMessageSigner Signer { get; }
	private ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> Clients { get; }
	private ConcurrentDictionary<uint256, ConcurrentDictionary<AffiliationFlag, byte[]>> CoinJoinRequests { get; } = new();
	private ConcurrentDictionary<uint256, RoundData> RoundData { get; } = new();
	private AsyncQueue<FinalizedRoundDataWithRoundId> RoundsToUpdate { get; } = new();

	public ImmutableDictionary<string, ImmutableDictionary<AffiliationFlag, byte[]>> GetCoinjoinRequests()
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
					Logging.Logger.LogError(exception);
				}
			}
		}
		catch (TaskCanceledException)
		{
		}
	}

	private async Task UpdateCoinJoinRequestsAsync(uint256 roundId, FinalizedRoundData finalizedRoundData, CancellationToken cancellationToken)
	{
		if (!CoinJoinRequests.TryAdd(roundId, new ConcurrentDictionary<AffiliationFlag, byte[]>()))
		{
			throw new InvalidOperationException();
		}
		await Task.WhenAll(Clients.Select(x => UpdateCoinJoinRequestsAsync(roundId, finalizedRoundData, x.Key, x.Value, cancellationToken))).ConfigureAwait(false);
	}

	private async Task UpdateCoinJoinRequestsAsync(uint256 roundId, FinalizedRoundData finalizedRoundData, AffiliationFlag affiliationFlag, AffiliateServerHttpApiClient affiliateServerHttpApiClient, CancellationToken cancellationToken)
	{
		try
		{
			Body body = finalizedRoundData.GetAffiliationData(affiliationFlag);
			byte[] result = await GetCoinJoinRequestAsync(affiliateServerHttpApiClient, body, cancellationToken).ConfigureAwait(false);

			if (CoinJoinRequests.TryGetValue(roundId, out ConcurrentDictionary<AffiliationFlag, byte[]>? coinjoinRequests))
			{
				if (!coinjoinRequests.TryAdd(affiliationFlag, result))
				{
					throw new InvalidOperationException("The coinjoin request is already set.");
				}
			}
			else
			{
				// This can occur if the round is finished before coinjoin requests are updated.
				Logging.Logger.LogInfo($"The round ({roundId}) does not exist.");
			}
		}
		catch (Exception exception)
		{
			Logging.Logger.LogError($"Cannot update coinjoin request for round ({roundId}) and affiliate flag '{affiliationFlag}': {exception}");
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
			Logging.Logger.LogInfo($"The round ({roundId}) does not exist.");
		}
	}

	private async Task<byte[]> GetCoinJoinRequestAsync(AffiliateServerHttpApiClient client, Body body, CancellationToken cancellationToken)
	{
		Payload payload = new(new Header(), body);
		byte[] signature = Signer.Sign(payload.GetCanonicalSerialization());
		GetCoinjoinRequestRequest coinjoinRequestRequest = new(body, signature);
		return await GetCoinJoinRequestAsync(client, coinjoinRequestRequest, cancellationToken).ConfigureAwait(false);
	}

	private async Task<byte[]> GetCoinJoinRequestAsync(AffiliateServerHttpApiClient client, GetCoinjoinRequestRequest getCoinjoinRequestRequest, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = new(AffiliateServerTimeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
		GetCoinjoinRequestResponse getCoinJoinRequestResponse = await client.GetCoinjoinRequestAsync(getCoinjoinRequestRequest, linkedCts.Token).ConfigureAwait(false);
		return getCoinJoinRequestResponse.CoinjoinRequest;
	}

	private void AddCoin(uint256 roundId, Coin coin, bool isCoordinationFeeExempted)
	{
		if (!RoundData.TryGetValue(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		roundData.AddInputCoin(coin);
		roundData.AddInputFeeExemption(coin, isCoordinationFeeExempted);
	}

	private void AddAffiliation(uint256 roundId, Coin coin, AffiliationFlag affiliationFlag)
	{
		if (!RoundData.TryGetValue(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		roundData.AddInputAffiliationFlag(coin, affiliationFlag);
	}

	private void AddCoinjoinTransaction(uint256 roundId, Transaction transaction)
	{
		if (!RoundData.TryGetValue(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		RoundsToUpdate.Enqueue(new FinalizedRoundDataWithRoundId(roundId, roundData.FinalizeRoundData(transaction)));
	}

	private void CreateRound(uint256 roundId, RoundParameters roundParameters)
	{
		RoundData roundData = new(roundParameters);

		if (!RoundData.TryAdd(roundId, roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) already exist.");
		}
	}

	private void ChangePhase(uint256 roundId, Phase phase)
	{
		if (phase == Phase.Ended)
		{
			RemoveRound(roundId);
		}
	}

	private void Arena_InputAdded(object? sender, InputAddedEventArgs inputAddedEventArgs)
	{
		uint256 roundId = inputAddedEventArgs.RoundId;
		Coin coin = inputAddedEventArgs.Coin;
		bool isCoordinationFeeExempted = inputAddedEventArgs.IsCoordinationFeeExempted;

		AddCoin(roundId, coin, isCoordinationFeeExempted);
	}

	private void Arena_AffiliationAdded(object? sender, AffiliationAddedEventArgs affiliationAddedEventArgs)
	{
		uint256 roundId = affiliationAddedEventArgs.RoundId;
		Coin coin = affiliationAddedEventArgs.Coin;
		AffiliationFlag affiliationFlag = affiliationAddedEventArgs.AffiliationFlag;

		AddAffiliation(roundId, coin, affiliationFlag);
	}

	private void Arena_RoundCreated(object? sender, RoundCreatedEventArgs roundCreatedEventArgs)
	{
		uint256 roundId = roundCreatedEventArgs.RoundId;
		RoundParameters roundParameters = roundCreatedEventArgs.RoundParameters;

		CreateRound(roundId, roundParameters);
	}

	private void ArenaCoinJoinTransactionAdded(object? sender, CoinJoinTransactionCreatedEventArgs coinJoinTransactionCreatedEventArgs)
	{
		uint256 roundId = coinJoinTransactionCreatedEventArgs.RoundId;
		Transaction transaction = coinJoinTransactionCreatedEventArgs.Transaction;

		AddCoinjoinTransaction(roundId, transaction);
	}

	private void Arena_RoundPhaseChanged(object? sender, RoundPhaseChangedEventArgs roundPhaseChangedEventArgs)
	{
		uint256 roundId = roundPhaseChangedEventArgs.RoundId;
		Phase phase = roundPhaseChangedEventArgs.Phase;

		ChangePhase(roundId, phase);
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
		base.Dispose();
	}

	private record FinalizedRoundDataWithRoundId(uint256 RoundId, FinalizedRoundData FinalizedRoundData);
}
