using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client.RoundStateAwaiters;

public class UnexpectedRoundPhaseException : Exception
{
	public UnexpectedRoundPhaseException(uint256 roundId, Phase expected, Phase actual)
	{
		RoundId = roundId;
		Expected = expected;
		Actual = actual;
	}

	public uint256 RoundId { get; }
	public Phase Expected { get; }
	public Phase Actual { get; }

	public override string Message => $"Round {RoundId} unexpected phase change. Waiting for '{Expected}' but the round is in '{Actual}'.";
}
