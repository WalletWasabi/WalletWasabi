using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;


public abstract record RoundUpdateMessage
{
	public record UpdateMessage(DateTime CurrentTime) : RoundUpdateMessage;
	public record CreateRoundAwaiter(uint256? RoundId, Phase? Phase, Predicate<RoundState>? Predicate, IReplyChannel<Task<RoundState>> ReplayChannel) : RoundUpdateMessage;
}

public class RoundStateProvider(MailboxProcessor<RoundUpdateMessage> roundStateUpdater)
{
	public static TimeSpan QueryFrequency = TimeSpan.FromSeconds(15);

	public async Task<RoundState> CreateRoundAwaiterAsync(uint256 roundId, Phase phase,
		CancellationToken cancellationToken)
	{
		var awaiter = await roundStateUpdater
			.PostAndReplyAsync<Task<RoundState>>(
				chan => new RoundUpdateMessage.CreateRoundAwaiter(roundId, phase, null, chan),
				cancellationToken).ConfigureAwait(false);
		using var cts =
			CancellationTokenSource.CreateLinkedTokenSource(roundStateUpdater.CancellationToken, cancellationToken);
		return await awaiter.WaitAsync(cts.Token).ConfigureAwait(false);
	}

	public async Task<RoundState> CreateRoundAwaiterAsync(Predicate<RoundState> predicate,
		CancellationToken cancellationToken)
	{
		var awaiter = await roundStateUpdater
			.PostAndReplyAsync<Task<RoundState>>(
				chan => new RoundUpdateMessage.CreateRoundAwaiter(null, null, predicate, chan),
				cancellationToken).ConfigureAwait(false);
		using var cts =
			CancellationTokenSource.CreateLinkedTokenSource(roundStateUpdater.CancellationToken, cancellationToken);
		return await awaiter.WaitAsync(cts.Token).ConfigureAwait(false);
	}
}

public record RoundsState(
	DateTime NextQueryTime,
	TimeSpan QueryInterval,
	Dictionary<uint256, RoundState> Rounds,
	ImmutableList<RoundStateAwaiter> Awaiters);

public static class RoundStateUpdater
{
	public static MessageHandler<RoundUpdateMessage, RoundsState> Create(IWabiSabiApiRequestHandler arenaRequestHandler) =>
		(msg, state, cancellationToken) => ProcessMessageAsync(msg, state, arenaRequestHandler, cancellationToken);

	private static async Task<RoundsState> ProcessMessageAsync(
		RoundUpdateMessage msg,
		RoundsState state,
		IWabiSabiApiRequestHandler arenaRequestHandler,
		CancellationToken cancellationToken)
	{
		switch (msg)
		{
			case RoundUpdateMessage.UpdateMessage m:
				if (state.Awaiters.Count > 0 && DateTime.UtcNow >= state.NextQueryTime)
				{
					var (rounds, awaiters) = await UpdateRoundsStateAsync(state, arenaRequestHandler, cancellationToken).ConfigureAwait(false);
					state = state with
					{
						NextQueryTime = m.CurrentTime + state.QueryInterval,
						Rounds = rounds,
						Awaiters = awaiters
					};
				}

				break;
			case RoundUpdateMessage.CreateRoundAwaiter m:
				var roundStateAwaiter = new RoundStateAwaiter(m.Predicate, m.RoundId, m.Phase, cancellationToken);
				state = state with {Awaiters = state.Awaiters.Add(roundStateAwaiter)};
				m.ReplayChannel.Reply(roundStateAwaiter.Task);
				break;
		}

		return state;
	}

	private static async Task<(Dictionary<uint256, RoundState> Rounds, ImmutableList<RoundStateAwaiter> Awaiters)> UpdateRoundsStateAsync(
		RoundsState state,
		IWabiSabiApiRequestHandler arenaRequestHandler,
		CancellationToken cancellationToken)
	{
		var request = new RoundStateRequest(
			state.Rounds.Select(x => new RoundStateCheckpoint(x.Key, x.Value.CoinjoinState.Events.Count)).ToImmutableList());

		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		var response = await arenaRequestHandler.GetStatusAsync(request, linkedCts.Token).ConfigureAwait(false);
		RoundState[] roundStates = response.RoundStates;

		var updatedRoundStates = roundStates
			.Where(rs => state.Rounds.ContainsKey(rs.Id))
			.Select(rs => (NewRoundState: rs, CurrentRoundState: state.Rounds[rs.Id]))
			.Select(x => x.NewRoundState with { CoinjoinState = x.NewRoundState.CoinjoinState.AddPreviousStates(x.CurrentRoundState.CoinjoinState, x.NewRoundState.Id) })
			.ToList();

		var newRoundStates = roundStates
			.Where(rs => !state.Rounds.ContainsKey(rs.Id));

		if (newRoundStates.Any(r => !r.IsRoundIdMatching()))
		{
			throw new InvalidOperationException(
				"Coordinator is cheating by creating rounds that do not match the parameters.");
		}

		// Don't use ToImmutable dictionary, because that ruins the original order and makes the server unable to suggest a round preference.
		// ToDo: ToDictionary doesn't guarantee the order by design so .NET team might change this out of our feet, so there's room for improvement here.
		var finalRoundStates = newRoundStates.Concat(updatedRoundStates).ToDictionary(x => x.Id, x => x);

		var completedAwaiters = state.Awaiters.Where(awaiter => awaiter.IsCompleted(finalRoundStates)).ToArray();
		return (Rounds: finalRoundStates, Awaiters: state.Awaiters.RemoveRange(completedAwaiters));
	}
}
