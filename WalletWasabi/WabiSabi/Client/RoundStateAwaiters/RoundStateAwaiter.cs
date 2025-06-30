using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
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

		_taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_predicate = predicate;
		_roundId = roundId;
		_phase = phase;
		_cancellationTokenRegistration = cancellationToken.Register(Cancel);
	}

	private readonly TaskCompletionSource<RoundState> _taskCompletionSource;
	private readonly Predicate<RoundState>? _predicate;
	private readonly uint256? _roundId;
	private readonly Phase? _phase;
	private readonly CancellationTokenRegistration _cancellationTokenRegistration;

	public Task<RoundState> Task => _taskCompletionSource.Task;

	public bool IsCompleted(IDictionary<uint256, RoundState> allRoundStates)
	{
		if (Task.IsCompleted)
		{
			return true;
		}

		if (_roundId is not null && !allRoundStates.ContainsKey(_roundId))
		{
			_taskCompletionSource.TrySetException(new InvalidOperationException($"Round {_roundId} is not running anymore."));
			return true;
		}

		foreach (var roundState in allRoundStates.Values.ToArray())
		{
			if (_roundId is { })
			{
				if (roundState.Id != _roundId)
				{
					continue;
				}
			}

			if (_phase is { } expectedPhase)
			{
				if (roundState.Phase > expectedPhase)
				{
					_taskCompletionSource.TrySetException(new UnexpectedRoundPhaseException(_roundId ?? uint256.Zero, expectedPhase, roundState));
					return true;
				}

				if (roundState.Phase != expectedPhase)
				{
					continue;
				}
			}

			if (_predicate is { })
			{
				if (!_predicate(roundState))
				{
					continue;
				}
			}

			_taskCompletionSource.SetResult(roundState);
			return true;
		}

		return false;
	}

	private void Cancel()
	{
		_taskCompletionSource.TrySetCanceled();
	}
}
