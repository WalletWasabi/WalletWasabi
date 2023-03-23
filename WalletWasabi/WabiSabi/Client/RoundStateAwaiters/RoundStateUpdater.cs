using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class RoundStateUpdater : PeriodicRunner
{
	public RoundStateUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler) : base(requestInterval)
	{
		ArenaRequestHandler = arenaRequestHandler;
	}

	private IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
	private IDictionary<uint256, RoundState> RoundStates { get; set; } = new Dictionary<uint256, RoundState>();
	public Dictionary<TimeSpan, FeeRate> CoinJoinFeeRateMedians { get; private set; } = new();

	private List<RoundStateAwaiter> Awaiters { get; } = new();
	private object AwaitersLock { get; } = new();

	public bool AnyRound => RoundStates.Any();

	public bool SlowRequestsMode { get; set; } = true;

	private DateTimeOffset LastSuccessfulRequestTime { get; set; }

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		if (SlowRequestsMode)
		{
			lock (AwaitersLock)
			{
				if (Awaiters.Count == 0 && DateTimeOffset.UtcNow - LastSuccessfulRequestTime < TimeSpan.FromMinutes(5))
				{
					return;
				}
			}
		}

		var request = new RoundStateRequest(
			RoundStates.Select(x => new RoundStateCheckpoint(x.Key, x.Value.CoinjoinState.Events.Count)).ToImmutableList());

		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		var response = await ArenaRequestHandler.GetStatusAsync(request, linkedCts.Token).ConfigureAwait(false);
		RoundState[] roundStates = response.RoundStates;

		CoinJoinFeeRateMedians = response.CoinJoinFeeRateMedians.ToDictionary(a => a.TimeFrame, a => a.MedianFeeRate);

		var updatedRoundStates = roundStates
			.Where(rs => RoundStates.ContainsKey(rs.Id))
			.Select(rs => (NewRoundState: rs, CurrentRoundState: RoundStates[rs.Id]))
			.Select(x => x.NewRoundState with { CoinjoinState = x.NewRoundState.CoinjoinState.AddPreviousStates(x.CurrentRoundState.CoinjoinState) })
			.ToList();

		var newRoundStates = roundStates
			.Where(rs => !RoundStates.ContainsKey(rs.Id));

		// Don't use ToImmutable dictionary, because that ruins the original order and makes the server unable to suggest a round preference.
		// ToDo: ToDictionary doesn't guarantee the order by design so .NET team might change this out of our feet, so there's room for improvement here.
		RoundStates = newRoundStates.Concat(updatedRoundStates).ToDictionary(x => x.Id, x => x);

		lock (AwaitersLock)
		{
			foreach (var awaiter in Awaiters.Where(awaiter => awaiter.IsCompleted(RoundStates)).ToArray())
			{
				// The predicate was fulfilled.
				Awaiters.Remove(awaiter);
				break;
			}
		}

		LastSuccessfulRequestTime = DateTimeOffset.UtcNow;
	}

	private Task<RoundState> CreateRoundAwaiterAsync(uint256? roundId, Phase? phase, Predicate<RoundState>? predicate, CancellationToken cancellationToken)
	{
		RoundStateAwaiter? roundStateAwaiter = null;

		lock (AwaitersLock)
		{
			roundStateAwaiter = new RoundStateAwaiter(predicate, roundId, phase, cancellationToken);
			Awaiters.Add(roundStateAwaiter);
		}

		cancellationToken.Register(() =>
		{
			lock (AwaitersLock)
			{
				Awaiters.Remove(roundStateAwaiter);
			}
		});

		return roundStateAwaiter.Task;
	}

	public Task<RoundState> CreateRoundAwaiterAsync(Predicate<RoundState> predicate, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiterAsync(null, null, predicate, cancellationToken);
	}

	public Task<RoundState> CreateRoundAwaiterAsync(uint256 roundId, Phase phase, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiterAsync(roundId, phase, null, cancellationToken);
	}

	public Task<RoundState> CreateRoundAwaiter(Phase phase, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiterAsync(null, phase, null, cancellationToken);
	}

	/// <summary>
	/// This might not contain up-to-date states. Make sure it is updated.
	/// </summary>
	public bool TryGetRoundState(uint256 roundId, [NotNullWhen(true)] out RoundState? roundState)
	{
		return RoundStates.TryGetValue(roundId, out roundState);
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		lock (AwaitersLock)
		{
			foreach (var awaiter in Awaiters)
			{
				awaiter.Cancel();
			}
		}
		return base.StopAsync(cancellationToken);
	}
}
