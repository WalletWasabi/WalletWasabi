using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
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
	public static MessageHandler<RoundUpdateMessage, RoundsState> Create(
		IWabiSabiApiRequestHandler arenaRequestHandler,
		IReadOnlyList<IWabiSabiApiRequestHandler>? verificationHandlers = null) =>
		(msg, state, cancellationToken) => ProcessMessageAsync(msg, state, arenaRequestHandler, verificationHandlers ?? Array.Empty<IWabiSabiApiRequestHandler>(), cancellationToken);

	private static async Task<RoundsState> ProcessMessageAsync(
		RoundUpdateMessage msg,
		RoundsState state,
		IWabiSabiApiRequestHandler arenaRequestHandler,
		IReadOnlyList<IWabiSabiApiRequestHandler> verificationHandlers,
		CancellationToken cancellationToken)
	{
		switch (msg)
		{
			case RoundUpdateMessage.UpdateMessage m:
				if (state.Awaiters.Count > 0 && DateTime.UtcNow >= state.NextQueryTime)
				{
					var (rounds, awaiters) = await UpdateRoundsStateAsync(state, arenaRequestHandler, verificationHandlers, cancellationToken).ConfigureAwait(false);
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
		IReadOnlyList<IWabiSabiApiRequestHandler> verificationHandlers,
		CancellationToken cancellationToken)
	{
		var request = new RoundStateRequest(
			state.Rounds.Select(x => new RoundStateCheckpoint(x.Key, x.Value.CoinjoinState.Events.Count)).ToImmutableList());

		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(30));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

		var response = await arenaRequestHandler.GetStatusAsync(request, linkedCts.Token).ConfigureAwait(false);
		RoundState[] roundStates = response.RoundStates;

		// Verify consistency of round data across independent Tor circuits.
		if (verificationHandlers.Count > 0)
		{
			await VerifyConsistencyAsync(roundStates, verificationHandlers, cancellationToken).ConfigureAwait(false);
		}

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

	// Maximum random delay (in milliseconds) before each verification query.
	// Each query gets an independent random delay to prevent timing correlation
	// between the primary poll and verification queries across Tor circuits.
	internal static int MaxVerificationDelayMs = 5000;

	private static async Task VerifyConsistencyAsync(
		RoundState[] primaryRoundStates,
		IReadOnlyList<IWabiSabiApiRequestHandler> verificationHandlers,
		CancellationToken cancellationToken)
	{
		// Use an empty request to avoid revealing which rounds this client tracks.
		var verificationRequest = RoundStateRequest.Empty;

		using var verificationTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, verificationTimeoutCts.Token);

		// Each verification query gets an independent random delay to break
		// timing correlation between the primary poll and verification queries.
		var verificationTasks = verificationHandlers
			.Select(handler => GetVerificationResponseAsync(handler, verificationRequest, linkedCts.Token))
			.ToArray();

		var verificationResults = await Task.WhenAll(verificationTasks).ConfigureAwait(false);

		var successfulResults = verificationResults
			.Where(r => r is not null)
			.ToArray();

		Logger.LogInfo($"Round state verification: {successfulResults.Length}/{verificationHandlers.Count} queries succeeded. Primary has {primaryRoundStates.Length} rounds.");

		if (successfulResults.Length == 0 && verificationHandlers.Count > 0)
		{
			Logger.LogWarning("All round state verification queries failed. Cannot verify coordinator consistency.");
			return;
		}

		foreach (var verificationRoundStates in successfulResults)
		{
			CheckConsistency(primaryRoundStates, verificationRoundStates!);
		}
	}

	private static async Task<RoundState[]?> GetVerificationResponseAsync(
		IWabiSabiApiRequestHandler handler,
		RoundStateRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			// Random delay before each query to prevent a coordinator from correlating
			// verification circuits with the primary circuit via timing analysis.
			var delayMs = Random.Shared.Next(MaxVerificationDelayMs);
			await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);

			var response = await handler.GetStatusAsync(request, cancellationToken).ConfigureAwait(false);
			return response.RoundStates;
		}
		catch (OperationCanceledException)
		{
			Logger.LogDebug("Round state verification query timed out or was cancelled.");
			return null;
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Round state verification query failed: {ex.Message}");
			return null;
		}
	}

	internal static void CheckConsistency(RoundState[] primaryStates, RoundState[] verificationStates)
	{
		var primaryById = primaryStates.ToDictionary(r => r.Id);
		var verificationById = verificationStates.ToDictionary(r => r.Id);

		// Rounds shown to our primary circuit but hidden from the verification circuit
		// are the signature of a partitioning attack.
		var suspiciouslyMissing = primaryById.Keys.Where(id => !verificationById.ContainsKey(id)).ToList();
		if (suspiciouslyMissing.Count > 0)
		{
			var msg = $"Round ID inconsistency: rounds [{string.Join(", ", suspiciouslyMissing)}] " +
				"were shown to primary circuit but not to verification circuit.";
			Logger.LogError(msg);
			throw new InvalidOperationException($"Coordinator served inconsistent round data across Tor circuits. {msg}");
		}

		// For rounds present in both, compare immutable structural fields.
		foreach (var roundId in primaryById.Keys.Where(id => verificationById.ContainsKey(id)))
		{
			var primary = primaryById[roundId];
			var verification = verificationById[roundId];

			if (primary.BlameOf != verification.BlameOf)
			{
				ThrowInconsistency(roundId, nameof(RoundState.BlameOf), primary.BlameOf, verification.BlameOf);
			}

			if (primary.AmountCredentialIssuerParameters.Cw != verification.AmountCredentialIssuerParameters.Cw ||
				primary.AmountCredentialIssuerParameters.I != verification.AmountCredentialIssuerParameters.I)
			{
				ThrowInconsistency(roundId, nameof(RoundState.AmountCredentialIssuerParameters));
			}

			if (primary.VsizeCredentialIssuerParameters.Cw != verification.VsizeCredentialIssuerParameters.Cw ||
				primary.VsizeCredentialIssuerParameters.I != verification.VsizeCredentialIssuerParameters.I)
			{
				ThrowInconsistency(roundId, nameof(RoundState.VsizeCredentialIssuerParameters));
			}

			if (primary.InputRegistrationStart != verification.InputRegistrationStart)
			{
				ThrowInconsistency(roundId, nameof(RoundState.InputRegistrationStart), primary.InputRegistrationStart, verification.InputRegistrationStart);
			}

			if (primary.InputRegistrationTimeout != verification.InputRegistrationTimeout)
			{
				ThrowInconsistency(roundId, nameof(RoundState.InputRegistrationTimeout), primary.InputRegistrationTimeout, verification.InputRegistrationTimeout);
			}

			// Allow ±1 phase tolerance for legitimate race conditions between queries.
			var phaseDiff = Math.Abs((int)primary.Phase - (int)verification.Phase);
			if (phaseDiff > 1)
			{
				ThrowInconsistency(roundId, nameof(RoundState.Phase), primary.Phase, verification.Phase);
			}
		}
	}

	private static void ThrowInconsistency(uint256 roundId, string fieldName, object? primary = null, object? verification = null)
	{
		var detail = primary is not null ? $" Primary: {primary}, Verification: {verification}" : "";
		var msg = $"Field '{fieldName}' mismatch for round {roundId}.{detail}";
		Logger.LogError($"Coordinator consistency check failed: {msg}");
		throw new InvalidOperationException(
			$"Coordinator served inconsistent round data across Tor circuits. {msg}");
	}
}
