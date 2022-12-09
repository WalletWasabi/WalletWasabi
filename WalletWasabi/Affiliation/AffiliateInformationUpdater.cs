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
using WalletWasabi.Affiliation.Models.PaymentData;

namespace WalletWasabi.Affiliation;

public class AffiliateInformationUpdater : PeriodicRunner
{
	private static TimeSpan AffiliateServerTimeout = TimeSpan.FromSeconds(60);
	private static TimeSpan Interval = TimeSpan.FromSeconds(1);

	public AffiliateInformationUpdater(Arena arena, ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> clients, Signer signer)
		  : base(Interval)
	{
		Arena = arena;
		Clients = clients;
		Signer = signer;

		PaymentData = new();
		RoundData = new();
		RoundsToUpdate = new();
		RoundsToRemove = new();

		AddHandlers(arena);
	}

	private Arena Arena { get; }
	private Signer Signer { get; }
	private ImmutableDictionary<AffiliationFlag, AffiliateServerHttpApiClient> Clients { get; }
	private Dictionary<uint256, Dictionary<AffiliationFlag, byte[]>> PaymentData { get; }
	private ConcurrentDictionary<uint256, RoundData> RoundData { get; }
	private Queue<uint256> RoundsToUpdate { get; }
	private Queue<uint256> RoundsToRemove { get; }

	public override void Dispose()
	{
		RemoveHandlers(Arena);
	}

	public IReadOnlyDictionary<uint256, IReadOnlyDictionary<AffiliationFlag, byte[]>> GetPaymentData()
	{
		return (IReadOnlyDictionary<uint256, IReadOnlyDictionary<AffiliationFlag, byte[]>>)PaymentData.ToDictionary(x => x.Key, x => (IReadOnlyDictionary<AffiliationFlag, byte[]>)x.Value);
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		await UpdatePaymentDataAsync();
		RemoveRounds();
	}

	private async Task UpdatePaymentDataAsync(uint256 roundId, RoundData roundData, AffiliationFlag affiliationFlag, AffiliateServerHttpApiClient affiliateServerHttpApiClient)
	{
		Body body = roundData.GetAffiliationData(affiliationFlag);
		byte[]? result = await GetPaymentDataAsync(affiliateServerHttpApiClient, body);

		if (result is not null)
		{
			if (!PaymentData.ContainsKey(roundId))
			{
				PaymentData.Add(roundId, new());
			}

			Dictionary<AffiliationFlag, byte[]> paymentData = PaymentData[roundId];

			if (paymentData.ContainsKey(affiliationFlag))
			{
				throw new InvalidOperationException("The payment data is already set.");
			}

			paymentData.Add(affiliationFlag, result);
		}
		else
		{
			Logging.Logger.LogWarning($"Cannot get payment data from affiliate server '{affiliationFlag}'.");
		}
	}

	private async Task UpdatePaymentDataAsync(uint256 roundId)
	{
		if (!RoundData.TryGetValue(roundId, out RoundData? roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		roundData.Lock();

		await Task.WhenAll(Clients.Select(x => UpdatePaymentDataAsync(roundId, roundData, x.Key, x.Value)));
	}

	private async Task UpdatePaymentDataAsync()
	{
		while (RoundsToUpdate.Any())
		{
			try
			{
				uint256 roundId = RoundsToUpdate.Dequeue();
				if (!RoundsToRemove.Contains(roundId))
				{
					await UpdatePaymentDataAsync(roundId);
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
		if (!RoundData.TryRemove(roundId, out var roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		if (PaymentData.ContainsKey(roundId))
		{
			PaymentData.Remove(roundId);
		}
		else
		{
			// This can occurd if the round is finished before payment data is updated
			Logging.Logger.LogInfo($"The round ({roundId}) does not exist.");
		}
	}

	private void RemoveRounds()
	{
		while (RoundsToRemove.Any())
		{
			try
			{
				uint256 roundId = RoundsToRemove.Dequeue();
				RemoveRound(roundId);
			}
			catch (Exception exception)
			{
				Logging.Logger.LogError(exception.Message);
			}
		}
	}

	private async Task<byte[]?> GetPaymentDataAsync(AffiliateServerHttpApiClient client, Body body)
	{
		Payload payload = new(new Header(), body);
		byte[] signature = Signer.Sign(payload.GetCanonicalSerialization());
		PaymentDataRequest paymentDataRequest = new(body, signature);
		return await GetPaymentDataAsync(client, paymentDataRequest);
	}

	private async Task<byte[]?> GetPaymentDataAsync(AffiliateServerHttpApiClient client, PaymentDataRequest paymentDataRequest)
	{
		using CancellationTokenSource cancellationTokenSource = new(AffiliateServerTimeout);
		CancellationToken cancellationToken = cancellationTokenSource.Token;
		return await GetPaymentDataAsync(client, paymentDataRequest, cancellationToken);
	}

	private async Task<byte[]?> GetPaymentDataAsync(AffiliateServerHttpApiClient client, PaymentDataRequest paymentDataRequest, CancellationToken cancellationToken)
	{
		try
		{
			PaymentDataResponse paymentDataResponse = await client.GetPaymentData(paymentDataRequest, cancellationToken);
			return paymentDataResponse.PaymentData;
		}
		catch (Exception)
		{
			return null;
		}
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
		RoundData roundData = new();
		roundData.AddRoundParameters(roundParameters);

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
		try
		{
			uint256 roundId = affiliationAddedEventArgs.RoundId;
			Coin coin = affiliationAddedEventArgs.Coin;
			AffiliationFlag affiliationFlag = affiliationAddedEventArgs.AffiliationFlag;
			bool isPayingZeroCoordinatrionFee = affiliationAddedEventArgs.IsPayingZeroCoordinatrionFee;

			AddAffiliation(roundId, coin, affiliationFlag, isPayingZeroCoordinatrionFee);
		}
		catch (Exception exception)
		{
			Logging.Logger.LogError(exception.Message);
		}
	}

	private void Arena_RoundCreated(object? sender, RoundCreatedEventArgs roundCreatedEventArgs)
	{
		try
		{
			uint256 roundId = roundCreatedEventArgs.RoundId;
			RoundParameters roundParameters = roundCreatedEventArgs.RoundParameters;

			CreateRound(roundId, roundParameters);
		}
		catch (Exception exception)
		{
			Logging.Logger.LogError(exception.Message);
		}
	}

	private void Arena_CoinjoinTransactionAdded(object? sender, CoinjoinTransactionCreatedEventArgs coinjoinTransactionCreatedEventArgs)
	{
		try
		{
			uint256 roundId = coinjoinTransactionCreatedEventArgs.RoundId;
			NBitcoin.Transaction transaction = coinjoinTransactionCreatedEventArgs.Transaction;

			AddCoinjoinTransaction(roundId, transaction);
		}
		catch (Exception exception)
		{
			Logging.Logger.LogError(exception.Message);
		}
	}

	private void Arena_RoundPhaseChanged(object? sender, RoundPhaseChangedEventArgs roundPhaseChangedEventArgs)
	{
		try
		{
			uint256 roundId = roundPhaseChangedEventArgs.RoundId;
			Phase phase = roundPhaseChangedEventArgs.Phase;

			ChangePhase(roundId, phase);
		}
		catch (Exception exception)
		{
			Logging.Logger.LogError(exception.Message);
		}
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
