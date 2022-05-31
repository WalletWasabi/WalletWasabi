using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Models;

public class WrongPhaseException : WabiSabiProtocolException
{
	public TimeSpan Late { get; }
	public Phase CurrentPhase { get; }
	public Phase[] ExpectedPhases { get; }
	public uint256 RoundId { get; }

	public WrongPhaseException(Round round, params Phase[] expectedPhases) 
		: base(WabiSabiProtocolErrorCode.WrongPhase, $"Round ({round.Id}): Wrong phase ({round.Phase}).")
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
		CurrentPhase = round.Phase;
		RoundId = round.Id;
		ExpectedPhases = expectedPhases;
	}
}
