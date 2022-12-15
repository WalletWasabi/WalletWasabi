using System.Linq;
using System.Collections.Immutable;
using System.Collections.Generic;
using WalletWasabi.Affiliation.Models;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Events;
using WalletWasabi.Bases;
using System.Collections.Concurrent;
using WalletWasabi.Affiliation.Models.CoinjoinRequest;

namespace WalletWasabi.Affiliation;

public class CoinjoinRequestsUpdater : PeriodicRunner
{
	private static readonly TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

	public CoinjoinRequestsUpdater(Arena arena, ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> clients, CoinJoinRequestRequestsSigner signer)
		  : base(Interval)
	{
		Arena = arena;
		Clients = clients;
		Signer = signer;

		CoinjoinRequests = new();
		RoundData = new();
		RoundsToUpdate = new();
		RoundsToRemove = new();

		AddHandlers(arena);
	}

	private Arena Arena { get; }
	private CoinJoinRequestRequestsSigner Signer { get; }
	private ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> Clients { get; }
	private Dictionary<uint256, Dictionary<AffiliationFlag, byte[]>> CoinjoinRequests { get; }
	private ConcurrentDictionary<uint256, RoundData> RoundData { get; }
	private Queue<uint256> RoundsToUpdate { get; }
	private Queue<uint256> RoundsToRemove { get; }

	public override void Dispose()
	{
		RemoveHandlers(Arena);
	}

	public ImmutableDictionary<uint256, ImmutableDictionary<AffiliationFlag, byte[]>> GetCoinjoinRequests()
	{
		return CoinjoinRequests.ToDictionary(x => x.Key, x => x.Value.ToImmutableDictionary()).ToImmutableDictionary();
	}

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		await UpdateCoinjoinRequestsAsync(cancellationToken).ConfigureAwait(false);
		RemoveRounds();
	}

	private async Task UpdateCoinjoinRequestsAsync(uint256 roundId, RoundData roundData, AffiliationFlag affiliationFlag, AffiliateServerHttpApiClient affiliateServerHttpApiClient, CancellationToken cancellationToken)
	{
		try
		{
			Body body = roundData.GetAffiliationData(affiliationFlag);
			byte[] result = await GetCoinjoinRequestAsync(affiliateServerHttpApiClient, body, cancellationToken).ConfigureAwait(false);

			if (!CoinjoinRequests.ContainsKey(roundId))
			{
				CoinjoinRequests.Add(roundId, new());
			}

			Dictionary<AffiliationFlag, byte[]> coinjoinRequests = CoinjoinRequests[roundId];

			if (!coinjoinRequests.TryAdd(affiliationFlag, result))
			{
				throw new InvalidOperationException("The coinjoin request is already set.");
			}
		}
		catch (Exception exception)
		{
			Logging.Logger.LogError($"Cannot update coinjoin request for round ({roundId}) and affiliate flag '{affiliationFlag}': {exception.Message}");
		}
	}

	private async Task UpdateCoinjoinRequestsAsync(uint256 roundId, CancellationToken cancellationToken)
	{
		if (!RoundData.TryGetValue(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		roundData.Lock();

		await Task.WhenAll(Clients.Select(x => UpdateCoinjoinRequestsAsync(roundId, roundData, x.Key, x.Value, cancellationToken))).ConfigureAwait(false);
	}

	private async Task UpdateCoinjoinRequestsAsync(CancellationToken cancellationToken)
	{
		while (RoundsToUpdate.TryDequeue(out uint256? roundId))
		{
			try
			{
				if (!RoundsToRemove.Contains(roundId))
				{
					await UpdateCoinjoinRequestsAsync(roundId, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception exception)
			{
				Logging.Logger.LogError(exception.Message);
			}
		}
	}

	private void RemoveRound(uint256 roundId)
	{
		if (!RoundData.TryRemove(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		if (!CoinjoinRequests.Remove(roundId))
		{
			// This can occur if the round is finished before coinjoin requests are updated.
			Logging.Logger.LogInfo($"The round ({roundId}) does not exist.");
		}
	}

	private void RemoveRounds()
	{
		while (RoundsToRemove.TryDequeue(out uint256 roundId))
		{
			try
			{
				RemoveRound(roundId);
			}
			catch (Exception exception)
			{
				Logging.Logger.LogError(exception.Message);
			}
		}
	}

	private async Task<byte[]> GetCoinjoinRequestAsync(AffiliateServerHttpApiClient client, Body body, CancellationToken cancellationToken)
	{
		Payload payload = new(new Header(), body);
		byte[] signature = Signer.Sign(payload.GetCanonicalSerialization());
		GetCoinjoinRequestRequest coinjoinRequestRequest = new(body, signature);
		return await GetCoinjoinRequestAsync(client, coinjoinRequestRequest, cancellationToken).ConfigureAwait(false);
	}

	private async Task<byte[]> GetCoinjoinRequestAsync(AffiliateServerHttpApiClient client, GetCoinjoinRequestRequest getCoinjoinRequestRequest, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCTS = new(AffiliateServerTimeout);
		using CancellationTokenSource linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCTS.Token);
		GetCoinjoinRequestResponse getCoinjoinRequestResponse = await client.GetCoinjoinRequestAsync(getCoinjoinRequestRequest, linkedCTS.Token).ConfigureAwait(false);
		return getCoinjoinRequestResponse.CoinjoinRequest;
	}

	private void AddAffiliation(uint256 roundId, Coin coin, AffiliationFlag affiliationFlag, bool isPayingZeroCoordinatrionFee)
	{
		if (!RoundData.TryGetValue(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		roundData.AddInput(coin, affiliationFlag, isPayingZeroCoordinatrionFee);
	}

	private void AddCoinjoinTransaction(uint256 roundId, NBitcoin.Transaction transaction)
	{
		if (!RoundData.TryGetValue(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		roundData.AddTransaction(transaction);
		RoundsToUpdate.Enqueue(roundId);
	}

	private void CreateRound(uint256 roundId, RoundParameters roundParameters)
	{
		RoundData roundData = new(roundParameters);

		if (!RoundData.TryAdd(roundId, roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}
	}

	private void ChangePhase(uint256 roundId, Phase phase)
	{
		if (phase == Phase.Ended)
		{
			RoundsToRemove.Enqueue(roundId);
		}
	}

	private void Arena_AffiliationAdded(object? sender, AffiliationAddedEventArgs affiliationAddedEventArgs)
	{
		uint256 roundId = affiliationAddedEventArgs.RoundId;
		Coin coin = affiliationAddedEventArgs.Coin;
		AffiliationFlag affiliationFlag = affiliationAddedEventArgs.AffiliationFlag;
		bool isPayingZeroCoordinatrionFee = affiliationAddedEventArgs.IsPayingZeroCoordinatrionFee;

		AddAffiliation(roundId, coin, affiliationFlag, isPayingZeroCoordinatrionFee);
	}

	private void Arena_RoundCreated(object? sender, RoundCreatedEventArgs roundCreatedEventArgs)
	{
		uint256 roundId = roundCreatedEventArgs.RoundId;
		RoundParameters roundParameters = roundCreatedEventArgs.RoundParameters;

		CreateRound(roundId, roundParameters);
	}

	private void Arena_CoinjoinTransactionAdded(object? sender, CoinjoinTransactionCreatedEventArgs coinjoinTransactionCreatedEventArgs)
	{
		uint256 roundId = coinjoinTransactionCreatedEventArgs.RoundId;
		NBitcoin.Transaction transaction = coinjoinTransactionCreatedEventArgs.Transaction;

		AddCoinjoinTransaction(roundId, transaction);
	}

	private void Arena_RoundPhaseChanged(object? sender, RoundPhaseChangedEventArgs roundPhaseChangedEventArgs)
	{
		uint256 roundId = roundPhaseChangedEventArgs.RoundId;
		Phase phase = roundPhaseChangedEventArgs.Phase;

		ChangePhase(roundId, phase);
	}

	private void AddHandlers(Arena arena)
	{
		Arena.RoundCreated += Arena_RoundCreated;
		Arena.AffiliationAdded += Arena_AffiliationAdded;
		Arena.CoinjoinTransactionCreated += Arena_CoinjoinTransactionAdded;
		Arena.RoundPhaseChanged += Arena_RoundPhaseChanged;
	}

	private void RemoveHandlers(Arena arena)
	{
		Arena.RoundCreated -= Arena_RoundCreated;
		Arena.AffiliationAdded -= Arena_AffiliationAdded;
		Arena.CoinjoinTransactionCreated -= Arena_CoinjoinTransactionAdded;
		Arena.RoundPhaseChanged -= Arena_RoundPhaseChanged;
	}
}
