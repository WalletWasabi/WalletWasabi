using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

public record RoundStateAwaiter
{
	public RoundStateAwaiter(
		Predicate<RoundState> predicate,
		uint256? roundId,
		CancellationToken cancellationToken)
	{
		TaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Predicate = predicate;
		RoundId = roundId;
		cancellationToken.Register(() => Cancel());
	}

	private TaskCompletionSource<RoundState> TaskCompletionSource { get; }
	private Predicate<RoundState> Predicate { get; }
	private uint256? RoundId { get; }

	public Task<RoundState> Task => TaskCompletionSource.Task;

	public bool IsCompleted(Dictionary<uint256, RoundState> allRoundStates)
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

		var relevantRoundStates = RoundId is null ? allRoundStates.Values.ToArray() : new[] { allRoundStates[RoundId] };

		foreach (var roundState in relevantRoundStates)
		{
			if (Predicate(roundState))
			{
				TaskCompletionSource.SetResult(roundState);
				return true;
			}
		}

		return false;
	}

	public void Cancel()
	{
		TaskCompletionSource.TrySetCanceled();
	}
}
