using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class RoundStateUpdater : PeriodicRunner
{
	public RoundStateUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler) : base(requestInterval)
	{
		_arenaRequestHandler = arenaRequestHandler;
	}

	private readonly IWabiSabiApiRequestHandler _arenaRequestHandler;
	private IDictionary<uint256, RoundState> RoundStates { get; set; } = new Dictionary<uint256, RoundState>();

	private readonly List<RoundStateAwaiter> _awaiters = new();
	private readonly object _awaitersLock = new();

	public bool AnyRound => RoundStates.Any();

	public bool SlowRequestsMode { get; set; } = true;

	private DateTimeOffset LastSuccessfulRequestTime { get; set; }

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		if (SlowRequestsMode)
		{
			lock (_awaitersLock)
			{
				if (_awaiters.Count == 0 && DateTimeOffset.UtcNow - LastSuccessfulRequestTime < TimeSpan.FromMinutes(5))
				{
					return;
				}
			}
		}

		var request = new RoundStateRequest(
			RoundStates.Select(x => new RoundStateCheckpoint(x.Key, x.Value.CoinjoinState.Events.Count)).ToImmutableList());

		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		var response = await _arenaRequestHandler.GetStatusAsync(request, linkedCts.Token).ConfigureAwait(false);
		RoundState[] roundStates = response.RoundStates;

		var updatedRoundStates = roundStates
			.Where(rs => RoundStates.ContainsKey(rs.Id))
			.Select(rs => (NewRoundState: rs, CurrentRoundState: RoundStates[rs.Id]))
			.Select(x => x.NewRoundState with { CoinjoinState = x.NewRoundState.CoinjoinState.AddPreviousStates(x.CurrentRoundState.CoinjoinState, x.NewRoundState.Id) })
			.ToList();

		var newRoundStates = roundStates
			.Where(rs => !RoundStates.ContainsKey(rs.Id));

		if (newRoundStates.Any(r => !r.IsRoundIdMatching()))
		{
			throw new InvalidOperationException(
				"Coordinator is cheating by creating rounds that do not match the parameters.");
		}

		// Don't use ToImmutable dictionary, because that ruins the original order and makes the server unable to suggest a round preference.
		// ToDo: ToDictionary doesn't guarantee the order by design so .NET team might change this out of our feet, so there's room for improvement here.
		RoundStates = newRoundStates.Concat(updatedRoundStates).ToDictionary(x => x.Id, x => x);

		lock (_awaitersLock)
		{
			foreach (var awaiter in _awaiters.Where(awaiter => awaiter.IsCompleted(RoundStates)).ToArray())
			{
				// The predicate was fulfilled.
				_awaiters.Remove(awaiter);
				break;
			}
		}

		LastSuccessfulRequestTime = DateTimeOffset.UtcNow;
	}

	public Task<RoundState> CreateRoundAwaiterAsync(Predicate<RoundState> predicate, CancellationToken cancellationToken)
	{
		RoundStateAwaiter roundStateAwaiter;

		lock (_awaitersLock)
		{
			roundStateAwaiter = new RoundStateAwaiter(predicate, cancellationToken);
			_awaiters.Add(roundStateAwaiter);
		}

		cancellationToken.Register(() =>
		{
			lock (_awaitersLock)
			{
				_awaiters.Remove(roundStateAwaiter);
			}
		});

		return roundStateAwaiter.Task;
	}

	public Task<RoundState> CreateRoundAwaiterAsync(uint256 roundId, Phase phase, CancellationToken cancellationToken) =>
		CreateRoundAwaiterAsync(rs => rs.Phase == phase && rs.Id == roundId, cancellationToken);

	/// <summary>
	/// This might not contain up-to-date states. Make sure it is updated.
	/// </summary>
	public bool TryGetRoundState(uint256 roundId, [NotNullWhen(true)] out RoundState? roundState)
	{
		return RoundStates.TryGetValue(roundId, out roundState);
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		lock (_awaitersLock)
		{
			foreach (var awaiter in _awaiters)
			{
				awaiter.Cancel();
			}
		}
		return base.StopAsync(cancellationToken);
	}
}
