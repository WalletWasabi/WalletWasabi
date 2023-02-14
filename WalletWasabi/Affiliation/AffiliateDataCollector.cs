using System.Collections.Generic;
using System.Threading;
using NBitcoin;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Events;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Affiliation;

// This is an extension of Arena. The internal state is updated as result of an event raised by Arena
// what means it's ALWAYS protected by the Arena lock and there is not possible concurrency conflicts.
// Additionally, all operations are synchronous
public class AffiliateDataCollector : IDisposable
{
	public AffiliateDataCollector(Arena arena)
	{
		Arena = arena;
		AddHandlers();
	}

	private Arena Arena { get; }
	private Dictionary<uint256, Dictionary<string, byte[]>> CoinJoinRequests { get; } = new();
	private Dictionary<uint256, RoundData> RoundData { get; } = new();
	private AsyncQueue<FinalizedRoundDataWithRoundId> RoundsToUpdate { get; } = new();

	public IAsyncEnumerable<FinalizedRoundDataWithRoundId> GetFinalizedRounds(CancellationToken cancellationToken) => 
		RoundsToUpdate.GetAsyncIterator(cancellationToken);
	
	private void RemoveRound(uint256 roundId)
	{
		if (!RoundData.Remove(roundId, out _))
		{
			throw new InvalidOperationException($"The round ({roundId}) does not exist.");
		}

		if (!CoinJoinRequests.Remove(roundId, out _))
		{
			// This can occur if the round is finished before coinjoin requests are updated.
			Logger.LogInfo($"The round ({roundId}) does not exist.");
		}
	}

	private void CreateRound(uint256 roundId, RoundParameters roundParameters)
	{
		RoundData roundData = new(roundParameters);

		if (!RoundData.TryAdd(roundId, roundData))
		{
			throw new InvalidOperationException($"The round ({roundId}) already exist.");
		}
	}

	private void Arena_InputAdded(object? sender, InputAddedEventArgs args) =>
		GetRoundDataOrFail(args.RoundId)
			.AddInputCoin(args.Coin, args.IsCoordinationFeeExempted);

	private void Arena_AffiliationAdded(object? sender, AffiliationAddedEventArgs args) =>
		GetRoundDataOrFail(args.RoundId)
			.AddInputAffiliationFlag(args.Coin, args.AffiliationFlag);

	private void Arena_RoundCreated(object? sender, RoundCreatedEventArgs args) =>
		CreateRound(args.RoundId, args.RoundParameters);

	private void ArenaCoinJoinTransactionAdded(object? sender, CoinJoinTransactionCreatedEventArgs args) =>
		RoundsToUpdate.Enqueue(
			new FinalizedRoundDataWithRoundId(args.RoundId, GetRoundDataOrFail(args.RoundId).FinalizeRoundData(args.Transaction)));

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

	public void Dispose()
	{
		RemoveHandlers();
	}
}
