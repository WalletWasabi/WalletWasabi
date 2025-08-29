using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public record RoundStateAwaiter
{
	public RoundStateAwaiter(Predicate<RoundState> predicate, CancellationToken cancellationToken)
	{
		_taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Task = _taskCompletionSource.Task.WaitAsync(cancellationToken);
		_predicate = predicate;
		cancellationToken.Register(Cancel);
	}

	private readonly TaskCompletionSource<RoundState> _taskCompletionSource;
	private readonly Predicate<RoundState> _predicate;

	public Task<RoundState> Task { get; }

	public bool IsCompleted(IDictionary<uint256, RoundState> allRoundStates)
	{
		if (Task.IsCompleted)
		{
			return true;
		}

		foreach (var roundState in allRoundStates.Values.ToArray())
		{
			if (!_predicate(roundState))
			{
				continue;
			}

			_taskCompletionSource.SetResult(roundState);
			return true;
		}

		return false;
	}

	public void Cancel()
	{
		_taskCompletionSource.TrySetCanceled();
	}
}
