using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.WabiSabi.Coordinator.Models;

public class WrongPhaseException : WabiSabiProtocolException
{
	public WrongPhaseException(Round round, params Phase[] expectedPhases)
		: base(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({round.Id}): Wrong phase ({round.Phase}).", exceptionData: new WrongPhaseExceptionData(round.Phase))
	{
		var latestExpectedPhase = expectedPhases.MaxBy(p => (int)p);
		var now = DateTimeOffset.UtcNow;

		var endTime = latestExpectedPhase switch
		{
			Phase.InputRegistration => round.InputRegistrationTimeFrame.EndTime,
			Phase.ConnectionConfirmation => round.ConnectionConfirmationTimeFrame.EndTime,
			Phase.OutputRegistration => round.OutputRegistrationTimeFrame.EndTime,
			Phase.TransactionSigning => round.TransactionSigningTimeFrame.EndTime,
			Phase.Ended => round.End,
			_ => throw new ArgumentException($"Unknown phase {latestExpectedPhase}.")
		};

		Late = now - endTime;

		PhaseTimeout = round.Phase switch
		{
			Phase.InputRegistration => round.InputRegistrationTimeFrame.Duration,
			Phase.ConnectionConfirmation => round.ConnectionConfirmationTimeFrame.Duration,
			Phase.OutputRegistration => round.OutputRegistrationTimeFrame.Duration,
			Phase.TransactionSigning => round.TransactionSigningTimeFrame.Duration,
			Phase.Ended => TimeSpan.Zero,
			_ => throw new ArgumentException($"Unknown phase {latestExpectedPhase}.")
		};

		CurrentPhase = round.Phase;
		RoundId = round.Id;
		ExpectedPhases = expectedPhases;
	}

	public TimeSpan Late { get; }
	public TimeSpan PhaseTimeout { get; }
	public Phase CurrentPhase { get; }
	public Phase[] ExpectedPhases { get; }
	public uint256 RoundId { get; }
}
