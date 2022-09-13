using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public record RoundStateAwaiter
{
	public RoundStateAwaiter(
		Predicate<RoundState>? predicate,
		uint256? roundId,
		Phase? phase,
		CancellationToken cancellationToken)
	{
		if (predicate is null && phase is null)
		{
			throw new ArgumentNullException(nameof(predicate));
		}

		TaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Predicate = predicate;
		RoundId = roundId;
		Phase = phase;
		cancellationToken.Register(() => Cancel());
	}

	private TaskCompletionSource<RoundState> TaskCompletionSource { get; }
	private Predicate<RoundState>? Predicate { get; }
	private uint256? RoundId { get; }
	private Phase? Phase { get; }

	public Task<RoundState> Task => TaskCompletionSource.Task;

	public bool IsCompleted(IDictionary<uint256, RoundState> allRoundStates)
	{
		if (Task.IsCompleted)
		{
			return true;
		}

		if (RoundId is not null && !allRoundStates.ContainsKey(RoundId))
		{
			TaskCompletionSource.TrySetException(new InvalidOperationException($"Round {RoundId} is not running anymore."));
			return true;
		}

		foreach (var roundState in allRoundStates.Values.ToArray())
		{
			if (RoundId is { })
			{
				if (roundState.Id != RoundId)
				{
					continue;
				}
			}

			if (Phase is { } expectedPhase)
			{
				if (roundState.Phase > expectedPhase)
				{
					TaskCompletionSource.TrySetException(new UnexpectedRoundPhaseException(RoundId ?? uint256.Zero, expectedPhase, roundState));
					return true;
				}

				if (roundState.Phase != expectedPhase)
				{
					continue;
				}
			}

			if (Predicate is { })
			{
				if (!Predicate(roundState))
				{
					continue;
				}
			}

			TaskCompletionSource.SetResult(roundState);
			return true;
		}

		return false;
	}

	public void Cancel()
	{
		TaskCompletionSource.TrySetCanceled();
	}
}
