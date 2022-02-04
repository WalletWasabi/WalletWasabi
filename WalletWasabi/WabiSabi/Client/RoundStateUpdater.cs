using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

public class RoundStateUpdater : PeriodicRunner
{
	public RoundStateUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler) : base(requestInterval)
	{
		ArenaRequestHandler = arenaRequestHandler;
	}

	private IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
	private Dictionary<uint256, RoundState> RoundStates { get; set; } = new();

	private List<RoundStateAwaiter> Awaiters { get; } = new();
	private object AwaitersLock { get; } = new();

	public bool AnyRound => RoundStates.Any();

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		var request = new RoundStateRequest(
			RoundStates.Select(x => new RoundStateCheckpoint(x.Key, x.Value.CoinjoinState.Events.Count)).ToImmutableList());

		RoundState[] statusResponse = await ArenaRequestHandler.GetStatusAsync(request, cancellationToken).ConfigureAwait(false);

		var updatedRoundStates = statusResponse
			.Where(rs => RoundStates.ContainsKey(rs.Id))
			.Select(rs => (NewRoundState: rs, CurrentRoundState: RoundStates[rs.Id]))
			.Select(x => x.NewRoundState with {
				CoinjoinState = x.NewRoundState.CoinjoinState.AddPreviousStates(x.CurrentRoundState.CoinjoinState)
			}).ToList();

		var newRoundStates = statusResponse
			.Where(rs => !RoundStates.ContainsKey(rs.Id));

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
	}

	private Task<RoundState> CreateRoundAwaiter(uint256? roundId, Phase? phase, Predicate<RoundState>? predicate, CancellationToken cancellationToken)
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

	public Task<RoundState> CreateRoundAwaiter(Predicate<RoundState> predicate, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiter(null, null, predicate, cancellationToken);
	}

	public Task<RoundState> CreateRoundAwaiter(uint256 roundId, Phase phase, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiter(roundId, phase, null, cancellationToken);
	}

	public Task<RoundState> CreateRoundAwaiter(Phase phase, CancellationToken cancellationToken)
	{
		return CreateRoundAwaiter(null, phase, null, cancellationToken);
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
