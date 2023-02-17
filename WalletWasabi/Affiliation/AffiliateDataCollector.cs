using System.Collections.Generic;
using System.Threading;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Events;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.Affiliation;

public interface IRoundNotifier : IDisposable
{
	IAsyncEnumerable<RoundNotification> GetRoundNotifications(CancellationToken cancellationToken);
}

public abstract record RoundNotification(uint256 RoundId);
public record RoundBuiltTransactionNotification(uint256 RoundId, BuiltTransactionData BuiltTransactionData) : RoundNotification(RoundId);
public record RoundEndedNotification(uint256 RoundId) : RoundNotification(RoundId);

// This is an extension of Arena. The internal state is updated as result of an event raised by Arena
// what means it's ALWAYS protected by the Arena lock and there is not possible concurrency conflicts.
// Additionally, all operations are synchronous
public class AffiliateDataCollector : IRoundNotifier
{
	public AffiliateDataCollector(Arena arena)
	{
		Arena = arena;
		AddHandlers();
	}

	private Arena Arena { get; }
	private Dictionary<uint256, RoundData> RoundData { get; } = new();
	private AsyncQueue<RoundNotification> RoundsToUpdate { get; } = new();

	public IAsyncEnumerable<RoundNotification> GetRoundNotifications(CancellationToken cancellationToken) => 
		RoundsToUpdate.GetAsyncIterator(cancellationToken);
	
	private void Arena_InputAdded(object? sender, InputAddedEventArgs args) =>
		GetRoundDataOrFail(args.RoundId)
			.AddInputCoin(args.Coin, args.IsCoordinationFeeExempted);

	private void Arena_AffiliationAdded(object? sender, AffiliationAddedEventArgs args) =>
		GetRoundDataOrFail(args.RoundId)
			.AddInputAffiliationId(args.Coin, args.AffiliationId);

	private void Arena_RoundCreated(object? sender, RoundCreatedEventArgs args)
	{
		if (!RoundData.TryAdd(args.RoundId, new(args.RoundParameters)))
		{
			throw new InvalidOperationException($"The round ({args.RoundId}) already exist.");
		}
	}

	private void ArenaCoinJoinTransactionAdded(object? sender, CoinJoinTransactionCreatedEventArgs args) =>
		RoundsToUpdate.Enqueue(
			new RoundBuiltTransactionNotification(args.RoundId, GetRoundDataOrFail(args.RoundId).FinalizeRoundData(args.Transaction)));

	private void Arena_RoundPhaseChanged(object? sender, RoundPhaseChangedEventArgs args)
	{
		if (args.Phase == Phase.Ended)
		{
			if (!RoundData.Remove(args.RoundId, out _))
			{
				throw new InvalidOperationException($"The round ({args.RoundId}) does not exist.");
			}

			RoundsToUpdate.Enqueue(new RoundEndedNotification(args.RoundId));
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
